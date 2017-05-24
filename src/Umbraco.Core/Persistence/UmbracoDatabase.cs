using System;
using System.Data;
using System.Data.Common;
using System.Text;
using NPoco;
using StackExchange.Profiling;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence.FaultHandling;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.SqlSyntax;

namespace Umbraco.Core.Persistence
{
    /// <summary>
    /// Extends NPoco Database for Umbraco.
    /// </summary>
    /// <remarks>
    /// <para>Is used everywhere in place of the original NPoco Database object, and provides additional features
    /// such as profiling, retry policies, logging, etc.</para>
    /// <para>Is never created directly but obtained from the <see cref="UmbracoDatabaseFactory"/>.</para>
    /// </remarks>
    public class UmbracoDatabase : Database, IUmbracoDatabase
    {
        // Umbraco's default isolation level is RepeatableRead
        private const IsolationLevel DefaultIsolationLevel = IsolationLevel.RepeatableRead;

        private readonly ILogger _logger;        
        private readonly RetryPolicy _connectionRetryPolicy;
        private readonly RetryPolicy _commandRetryPolicy;
        private readonly Guid _instanceGuid = Guid.NewGuid();

        #region Ctor

        /// <summary>
        /// Initializes a new instance of the <see cref="UmbracoDatabase"/> class.
        /// </summary>
        /// <remarks>
        /// <para>Used by UmbracoDatabaseFactory to create databases.</para>
        /// <para>Also used by DatabaseBuilder for creating databases and installing/upgrading.</para>
        /// </remarks>
        public UmbracoDatabase(string connectionString, SqlContext sqlContext, DbProviderFactory provider, ILogger logger, RetryPolicy connectionRetryPolicy = null, RetryPolicy commandRetryPolicy = null)
            : base(connectionString, sqlContext.DatabaseType, provider, DefaultIsolationLevel)
        {
            SqlContext = sqlContext;

            _logger = logger;
            _connectionRetryPolicy = connectionRetryPolicy;
            _commandRetryPolicy = commandRetryPolicy;

            EnableSqlTrace = EnableSqlTraceDefault;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UmbracoDatabase"/> class.
        /// </summary>
        /// <remarks>Internal for unit tests only.</remarks>
        internal UmbracoDatabase(DbConnection connection, SqlContext sqlContext, ILogger logger)
            : base(connection, sqlContext.DatabaseType, DefaultIsolationLevel)
        {
            SqlContext = sqlContext;
            _logger = logger;

            EnableSqlTrace = EnableSqlTraceDefault;
        }

        #endregion

        /// <inheritdoc />
        public SqlContext SqlContext { get; }

        /// <inheritdoc />
        public ISqlSyntaxProvider SqlSyntax => SqlContext.SqlSyntax;
        
        public Sql<SqlContext> Sql() => new Sql<SqlContext>(SqlContext);

        public Sql<SqlContext> Sql(string sql, params object[] args) => Sql().Append(sql, args);

        public IQuery<T> Query<T>() => new Query<T>(SqlContext);

        #region Testing, Debugging and Troubleshooting

        private bool _enableCount;

#if DEBUG_DATABASES
        private int _spid = -1;
        private const bool EnableSqlTraceDefault = true;
#else
        private string _instanceId;
        private const bool EnableSqlTraceDefault = false;
#endif

        /// <inheritdoc />
        public string InstanceId
        {
            get
            {
#if DEBUG_DATABASES
                return _instanceGuid.ToString("N").Substring(0, 8) + ':' + _spid;
#else
                return _instanceId ?? (_instanceId = _instanceGuid.ToString("N").Substring(0, 8));
#endif
            }
        }

        /// <inheritdoc />
        public bool InTransaction { get; private set; }

        protected override void OnBeginTransaction()
        {
            base.OnBeginTransaction();
            InTransaction = true;
        }

        protected override void OnAbortTransaction()
        {
            InTransaction = false;
            base.OnAbortTransaction();
        }

        protected override void OnCompleteTransaction()
        {
            InTransaction = false;
            base.OnCompleteTransaction();
        }

        /// <summary>
        /// Gets or sets a value indicating whether to log all executed Sql statements.
        /// </summary>
        internal bool EnableSqlTrace { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to count all executed Sql statements.
        /// </summary>
        internal bool EnableSqlCount
        {
            get { return _enableCount; }
            set
            {
                _enableCount = value;
                if (_enableCount == false)
                    SqlCount = 0;
            }
        }

        /// <summary>
        /// Gets the count of all executed Sql statements.
        /// </summary>
        internal int SqlCount { get; private set; }

        #endregion

        #region OnSomething

        // fixme.poco - has new interceptors to replace OnSomething?

        protected override DbConnection OnConnectionOpened(DbConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

#if DEBUG_DATABASES
            // determines the database connection SPID for debugging
            if (DatabaseType == DBType.MySql)
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT CONNECTION_ID()";
                    _spid = Convert.ToInt32(command.ExecuteScalar());
                }
            }
            else if (DatabaseType == DBType.SqlServer)
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT @@SPID";
                    _spid = Convert.ToInt32(command.ExecuteScalar());
                }
            }
            else
            {
                // includes SqlCE
                _spid = 0;
            }
#endif

            // wrap the connection with a profiling connection that tracks timings
            connection = new StackExchange.Profiling.Data.ProfiledDbConnection(connection, MiniProfiler.Current);

            // wrap the connection with a retrying connection
            if (_connectionRetryPolicy != null || _commandRetryPolicy != null)
                connection = new RetryDbConnection(connection, _connectionRetryPolicy, _commandRetryPolicy);

            return connection;
        }

#if DEBUG_DATABASES
        public override void OnConnectionClosing(IDbConnection conn)
        {
            _spid = -1;
            base.OnConnectionClosing(conn);
        }
#endif

        protected override void OnException(Exception x)
        {
            _logger.Error<UmbracoDatabase>("Exception (" + InstanceId + ").", x);
            base.OnException(x);
        }

        protected override void OnExecutingCommand(DbCommand cmd)
        {
            // if no timeout is specified, and the connection has a longer timeout, use it
            if (OneTimeCommandTimeout == 0 && CommandTimeout == 0 && cmd.Connection.ConnectionTimeout > 30)
                cmd.CommandTimeout = cmd.Connection.ConnectionTimeout;

            if (EnableSqlTrace)
            {
                var sb = new StringBuilder();
#if DEBUG_DATABASES
                sb.Append(InstanceId);
                sb.Append(": ");
#endif
                sb.Append(cmd.CommandText);
                foreach (DbParameter p in cmd.Parameters)
                {
                    sb.Append(" - ");
                    sb.Append(p.Value);
                }

                _logger.Debug<UmbracoDatabase>(sb.ToString().Replace("{", "{{").Replace("}", "}}"));
            }

#if DEBUG_DATABASES
            // detects whether the command is already in use (eg still has an open reader...)
            DatabaseDebugHelper.SetCommand(cmd, InstanceId + " [T" + Thread.CurrentThread.ManagedThreadId + "]");
            var refsobj = DatabaseDebugHelper.GetReferencedObjects(cmd.Connection);
            if (refsobj != null) _logger.Debug<UmbracoDatabase>("Oops!" + Environment.NewLine + refsobj);
#endif

            base.OnExecutingCommand(cmd);
        }

        protected override void OnExecutedCommand(DbCommand cmd)
        {
            if (_enableCount)
                SqlCount++;

            base.OnExecutedCommand(cmd);
        }

        #endregion
    }
}