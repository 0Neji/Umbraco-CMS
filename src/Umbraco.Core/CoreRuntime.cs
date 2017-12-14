﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Web;
using LightInject;
using Semver;
using Umbraco.Core.Cache;
using Umbraco.Core.Components;
using Umbraco.Core.Configuration;
using Umbraco.Core.Composing;
using Umbraco.Core.Exceptions;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.Rdbms;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Mappers;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.Scoping;
using Umbraco.Core.Services;

namespace Umbraco.Core
{
    /// <summary>
    /// Represents the Core Umbraco runtime.
    /// </summary>
    /// <remarks>Does not handle any of the web-related aspects of Umbraco (startup, etc). It
    /// should be possible to use this runtime in console apps.</remarks>
    public class CoreRuntime : IRuntime
    {
        private readonly UmbracoApplicationBase _app;
        private BootLoader _bootLoader;
        private RuntimeState _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoreRuntime"/> class.
        /// </summary>
        /// <param name="umbracoApplication">The Umbraco HttpApplication.</param>
        public CoreRuntime(UmbracoApplicationBase umbracoApplication)
        {
            _app = umbracoApplication ?? throw new ArgumentNullException(nameof(umbracoApplication));
        }

        /// <inheritdoc/>
        public virtual void Boot(ServiceContainer container)
        {
            // some components may want to initialize with the UmbracoApplicationBase
            // well, they should not - we should not do this - however, Compat7 wants
            // it, so let's do it, but we should remove this eventually.
            container.RegisterInstance(_app);

            Compose(container);

            // prepare essential stuff

            var path = GetApplicationRootPath();
            if (string.IsNullOrWhiteSpace(path) == false)
                IOHelper.SetRootDirectory(path);

            _state = (RuntimeState) container.GetInstance<IRuntimeState>();
            _state.Level = RuntimeLevel.Boot;

            Logger = container.GetInstance<ILogger>();
            Profiler = container.GetInstance<IProfiler>();
            ProfilingLogger = container.GetInstance<ProfilingLogger>();

            // the boot loader boots using a container scope, so anything that is PerScope will
            // be disposed after the boot loader has booted, and anything else will remain.
            // note that this REQUIRES that perWebRequestScope has NOT been enabled yet, else
            // the container will fail to create a scope since there is no http context when
            // the application starts.
            // the boot loader is kept in the runtime for as long as Umbraco runs, and components
            // are NOT disposed - which is not a big deal as long as they remain lightweight
            // objects.

            using (var bootTimer = ProfilingLogger.TraceDuration<CoreRuntime>(
                $"Booting Umbraco {UmbracoVersion.SemanticVersion.ToSemanticString()} on {NetworkHelper.MachineName}.",
                "Booted.",
                "Boot failed."))
            {
                // throws if not full-trust
                new AspNetHostingPermission(AspNetHostingPermissionLevel.Unrestricted).Demand();

                try
                {
                    Logger.Debug<CoreRuntime>($"Runtime: {GetType().FullName}");

                    AquireMainDom(container);
                    DetermineRuntimeLevel(container);
                    var componentTypes = ResolveComponentTypes();
                    _bootLoader = new BootLoader(container);
                    _bootLoader.Boot(componentTypes, _state.Level);
                }
                catch (Exception e)
                {
                    _state.Level = RuntimeLevel.BootFailed;
                    var bfe = e as BootFailedException ?? new BootFailedException("Boot failed.", e);
                    _state.BootFailedException = bfe;
                    bootTimer.Fail(exception: bfe); // be sure to log the exception - even if we repeat ourselves

                    // throwing here can cause w3wp to hard-crash and we want to avoid it.
                    // instead, we're logging the exception and setting level to BootFailed.
                    // various parts of Umbraco such as UmbracoModule and UmbracoDefaultOwinStartup
                    // understand this and will nullify themselves, while UmbracoModule will
                    // throw a BootFailedException for every requests.
                }
            }

            //fixme
            // after Umbraco has started there is a scope in "context" and that context is
            // going to stay there and never get destroyed nor reused, so we have to ensure that
            // everything is cleared
            //var sa = container.GetInstance<IDatabaseScopeAccessor>();
            //sa.Scope?.Dispose();
        }

        private void AquireMainDom(IServiceFactory container)
        {
            using (var timer = ProfilingLogger.DebugDuration<CoreRuntime>("Acquiring MainDom.", "Aquired."))
            {
                try
                {
                    var mainDom = container.GetInstance<MainDom>();
                    mainDom.Acquire();
                }
                catch
                {
                    timer.Fail();
                    throw;
                }
            }
        }

        // internal for tests
        internal void DetermineRuntimeLevel(IServiceFactory container)
        {
            using (var timer = ProfilingLogger.DebugDuration<CoreRuntime>("Determining runtime level.", "Determined."))
            {
                try
                {
                    var dbfactory = container.GetInstance<IUmbracoDatabaseFactory>();
                    SetRuntimeStateLevel(_state, dbfactory, Logger);
                    Logger.Debug<CoreRuntime>($"Runtime level: {_state.Level}");
                }
                catch
                {
                    timer.Fail();
                    throw;
                }
            }
        }

        private IEnumerable<Type> ResolveComponentTypes()
        {
            using (var timer = ProfilingLogger.TraceDuration<CoreRuntime>("Resolving component types.", "Resolved."))
            {
                try
                {
                    return GetComponentTypes();
                }
                catch
                {
                    timer.Fail();
                    throw;
                }
            }
        }

        /// <inheritdoc/>
        public virtual void Terminate()
        {
            using (ProfilingLogger.DebugDuration<CoreRuntime>("Terminating Umbraco.", "Terminated."))
            {
                _bootLoader?.Terminate();
            }
        }

        /// <inheritdoc/>
        public virtual void Compose(ServiceContainer container)
        {
            // compose the very essential things that are needed to bootstrap, before anything else,
            // and only these things - the rest should be composed in runtime components

            // register basic things
            container.RegisterSingleton<IProfiler, LogProfiler>();
            container.RegisterSingleton<ProfilingLogger>();
            container.RegisterSingleton<IRuntimeState, RuntimeState>();

            // register caches
            // need the deep clone runtime cache profiver to ensure entities are cached properly, ie
            // are cloned in and cloned out - no request-based cache here since no web-based context,
            // will be overriden later or
            container.RegisterSingleton(_ => new CacheHelper(
                new DeepCloneRuntimeCacheProvider(new ObjectCacheRuntimeCacheProvider()),
                new StaticCacheProvider(),
                new NullCacheProvider(),
                new IsolatedRuntimeCache(type => new DeepCloneRuntimeCacheProvider(new ObjectCacheRuntimeCacheProvider()))));
            container.RegisterSingleton(f => f.GetInstance<CacheHelper>().RuntimeCache);

            // register the plugin manager
            container.RegisterSingleton(f => new TypeLoader(f.GetInstance<IRuntimeCacheProvider>(), f.GetInstance<ProfilingLogger>()));

            // register syntax providers - required by database factory
            container.Register<ISqlSyntaxProvider, MySqlSyntaxProvider>("MySqlSyntaxProvider");
            container.Register<ISqlSyntaxProvider, SqlCeSyntaxProvider>("SqlCeSyntaxProvider");
            container.Register<ISqlSyntaxProvider, SqlServerSyntaxProvider>("SqlServerSyntaxProvider");

            // register persistence mappers - required by database factory so needs to be done here
            // means the only place the collection can be modified is in a runtime - afterwards it
            // has been frozen and it is too late
            var mapperCollectionBuilder = container.RegisterCollectionBuilder<MapperCollectionBuilder>();
            ComposeMapperCollection(mapperCollectionBuilder);

            // register database factory - required to check for migrations
            // will be initialized with syntax providers and a logger, and will try to configure
            // from the default connection string name, if possible, else will remain non-configured
            // until properly configured (eg when installing)
            container.RegisterSingleton<IUmbracoDatabaseFactory, UmbracoDatabaseFactory>();
            container.RegisterSingleton(f => f.GetInstance<IUmbracoDatabaseFactory>().SqlContext);

            // register the scope provider
            container.RegisterSingleton<ScopeProvider>();
            container.RegisterSingleton<IScopeProvider>(f => f.GetInstance<ScopeProvider>());
            container.RegisterSingleton<IScopeAccessor>(f => f.GetInstance<ScopeProvider>());

            // register MainDom
            container.RegisterSingleton<MainDom>();
        }

        protected virtual void ComposeMapperCollection(MapperCollectionBuilder builder)
        {
            builder.AddCore();
        }

        private void SetRuntimeStateLevel(RuntimeState runtimeState, IUmbracoDatabaseFactory databaseFactory, ILogger logger)
        {
            var localVersion = LocalVersion; // the local, files, version
            var codeVersion = runtimeState.SemanticVersion; // the executing code version
            var connect = false;

            // we don't know yet
            runtimeState.Level = RuntimeLevel.Unknown;

            if (string.IsNullOrWhiteSpace(localVersion))
            {
                // there is no local version, we are not installed
                logger.Debug<CoreRuntime>("No local version, need to install Umbraco.");
                runtimeState.Level = RuntimeLevel.Install;
            }
            else if (localVersion != codeVersion)
            {
                // there *is* a local version, but it does not match the code version
                // need to upgrade
                logger.Debug<CoreRuntime>($"Local version \"{localVersion}\" != code version, need to upgrade Umbraco.");
                runtimeState.Level = RuntimeLevel.Upgrade;
            }
            else if (databaseFactory.Configured == false)
            {
                // local version *does* match code version, but the database is not configured
                // install (again? this is a weird situation...)
                logger.Debug<CoreRuntime>("Database is not configured, need to install Umbraco.");
                runtimeState.Level = RuntimeLevel.Install;
            }

            // install? not going to test anything else
            if (runtimeState.Level == RuntimeLevel.Install)
                return;

            // else, keep going,
            // anything other than install wants a database - see if we can connect
            // (since this is an already existing database, assume localdb is ready)
            for (var i = 0; i < 5; i++)
            {
                connect = databaseFactory.CanConnect;
                if (connect) break;
                logger.Debug<CoreRuntime>(i == 0
                    ? "Could not immediately connect to database, trying again."
                    : "Could not connect to database.");
                Thread.Sleep(1000);
            }

            if (connect == false)
            {
                // cannot connect to configured database, this is bad, fail
                logger.Debug<CoreRuntime>("Could not connect to database.");
                runtimeState.Level = RuntimeLevel.BootFailed;

                // in fact, this is bad enough that we want to throw
                throw new BootFailedException("A connection string is configured but Umbraco could not connect to the database.");
            }

            // if we already know we want to upgrade, no need to look for migrations...
            if (runtimeState.Level == RuntimeLevel.Upgrade)
                return;

            // else
            // look for a matching migration entry - bypassing services entirely - they are not 'up' yet
            // fixme - in a LB scenario, ensure that the DB gets upgraded only once!
            // fixme - eventually move to yol-style guid-based transitions
            bool exists;
            try
            {
                exists = EnsureMigration(databaseFactory, codeVersion);
            }
            catch
            {
                // can connect to the database but cannot access the migration table... need to install
                logger.Debug<CoreRuntime>("Could not check migrations, need to install Umbraco.");
                runtimeState.Level = RuntimeLevel.Install;
                return;
            }

            if (exists)
            {
                // the database version matches the code & files version, all clear, can run
                runtimeState.Level = RuntimeLevel.Run;
                return;
            }

            // the db version does not match... but we do have a migration table
            // so, at least one valid table, so we quite probably are installed & need to upgrade

            // although the files version matches the code version, the database version does not
            // which means the local files have been upgraded but not the database - need to upgrade
            logger.Debug<CoreRuntime>("Database migrations have not executed, need to upgrade Umbraco.");
            runtimeState.Level = RuntimeLevel.Upgrade;
        }

        protected virtual bool EnsureMigration(IUmbracoDatabaseFactory databaseFactory, SemVersion codeVersion)
        {
            using (var database = databaseFactory.CreateDatabase()) // no scope - just the database
            {
                var codeVersionString = codeVersion.ToString();
                var sql = databaseFactory.SqlContext.Sql()
                    .Select<MigrationDto>()
                    .From<MigrationDto>()
                    .Where<MigrationDto>(x => x.Name.InvariantEquals(Constants.System.UmbracoMigrationName) && x.Version == codeVersionString);
                return database.FirstOrDefault<MigrationDto>(sql) != null;
            }
        }

        private static string LocalVersion
        {
            get
            {
                try
                {
                    // fixme - this should live in its own independent file! NOT web.config!
                    return ConfigurationManager.AppSettings["umbracoConfigurationStatus"];
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        #region Locals

        protected ILogger Logger { get; private set; }

        protected IProfiler Profiler { get; private set; }

        protected ProfilingLogger ProfilingLogger { get; private set; }

        #endregion

        #region Getters

        // getters can be implemented by runtimes inheriting from CoreRuntime

        // fixme - inject! no Current!
        protected virtual IEnumerable<Type> GetComponentTypes() => Current.TypeLoader.GetTypes<IUmbracoComponent>();

        // by default, returns null, meaning that Umbraco should auto-detect the application root path.
        // override and return the absolute path to the Umbraco site/solution, if needed
        protected virtual string GetApplicationRootPath() => null;

        #endregion
    }
}
