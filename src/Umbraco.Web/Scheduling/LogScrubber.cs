using System;
using System.Threading;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;
using Umbraco.Core.Services;
using Umbraco.Core.Sync;

namespace Umbraco.Web.Scheduling
{
    internal class LogScrubber : RecurringTaskBase
    {
        private readonly IRuntimeState _runtime;
        private readonly IAuditService _auditService;
        private readonly IUmbracoSettingsSection _settings;
        private readonly ILogger _logger;
        private readonly ProfilingLogger _proflog;
        private readonly IUmbracoDatabaseFactory _databaseFactory;

        public LogScrubber(IBackgroundTaskRunner<RecurringTaskBase> runner, int delayMilliseconds, int periodMilliseconds,
            IRuntimeState runtime, IAuditService auditService, IUmbracoSettingsSection settings, IUmbracoDatabaseFactory databaseFactory, ILogger logger, ProfilingLogger proflog)
            : base(runner, delayMilliseconds, periodMilliseconds)
        {
            _runtime = runtime;
            _auditService = auditService;
            _settings = settings;
            _databaseFactory = databaseFactory;
            _logger = logger;
            _proflog = proflog;
        }

        // maximum age, in minutes
        private int GetLogScrubbingMaximumAge(IUmbracoSettingsSection settings)
        {
            var maximumAge = 24 * 60; // 24 hours, in minutes
            try
            {
                if (settings.Logging.MaxLogAge > -1)
                    maximumAge = settings.Logging.MaxLogAge;
            }
            catch (Exception e)
            {
                _logger.Error<LogScrubber>("Unable to locate a log scrubbing maximum age. Defaulting to 24 hours.", e);
            }
            return maximumAge;

        }

        public static int GetLogScrubbingInterval(IUmbracoSettingsSection settings, ILogger logger)
        {
            var interval = 4 * 60 * 60 * 1000; // 4 hours, in milliseconds
            try
            {
                if (settings.Logging.CleaningMiliseconds > -1)
                    interval = settings.Logging.CleaningMiliseconds;
            }
            catch (Exception e)
            {
                logger.Error<LogScrubber>("Unable to locate a log scrubbing interval. Defaulting to 4 hours.", e);
            }
            return interval;
        }

        public override bool PerformRun()
        {
            switch (_runtime.ServerRole)
            {
                case ServerRole.Slave:
                    _logger.Debug<LogScrubber>("Does not run on slave servers.");
                    return true; // DO repeat, server role can change
                case ServerRole.Unknown:
                    _logger.Debug<LogScrubber>("Does not run on servers with unknown role.");
                    return true; // DO repeat, server role can change
            }

            // ensure we do not run if not main domain, but do NOT lock it
            if (_runtime.IsMainDom == false)
            {
                _logger.Debug<LogScrubber>("Does not run if not MainDom.");
                return false; // do NOT repeat, going down
            }

            // running on a background task, requires a database scope
            using (_databaseFactory.CreateScope())
            using (_proflog.DebugDuration<LogScrubber>("Log scrubbing executing", "Log scrubbing complete"))
            {
                _auditService.CleanLogs(GetLogScrubbingMaximumAge(_settings));
            }

            return true; // repeat
        }

        public override Task<bool> PerformRunAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override bool IsAsync
        {
            get { return false; }
        }

        public override bool RunsOnShutdown
        {
            get { return false; }
        }
    }
}