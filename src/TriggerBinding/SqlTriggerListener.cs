// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry;
using static Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry.Telemetry;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlTriggerConstants;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlBindingUtilities;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlTriggerUtils;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Represents the listener to SQL table changes.
    /// </summary>
    /// <typeparam name="T">POCO class representing the row in the user table</typeparam>
    internal sealed class SqlTriggerListener<T> : IListener, IScaleMonitorProvider, ITargetScalerProvider
    {
        private const int ListenerNotStarted = 0;
        private const int ListenerStarting = 1;
        private const int ListenerStarted = 2;
        private const int ListenerStopping = 3;
        private const int ListenerStopped = 4;
        private readonly SqlObject _userTable;
        private readonly string _connectionString;
        private readonly string _userDefinedLeasesTableName;
        /// <summary>
        /// The unique ID we'll use to identify this function in our global state tables
        /// </summary>
        private readonly string _userFunctionId;
        /// <summary>
        /// The unique function ID based on the host ID - this is used for backwards compatibility to
        /// ensure that users upgrading to the new WEBSITE_SITE_NAME based ID don't lose their state
        /// </summary>
        private readonly string _hostIdFunctionId;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly SqlOptions _sqlOptions;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        private readonly Dictionary<TelemetryPropertyName, string> _telemetryProps = new Dictionary<TelemetryPropertyName, string>();
        private readonly int _maxChangesPerWorker;
        private readonly bool _hasConfiguredMaxChangesPerWorker = false;

        private SqlTableChangeMonitor<T> _changeMonitor;
        private readonly IScaleMonitor<SqlTriggerMetrics> _scaleMonitor;
        private readonly ITargetScaler _targetScaler;

        private int _listenerState = ListenerNotStarted;


        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerListener{T}"/> class.
        /// </summary>
        /// <param name="connectionString">SQL connection string used to connect to user database</param>
        /// <param name="tableName">Name of the user table</param>
        /// <param name="userDefinedLeasesTableName">Optional - Name of the leases table</param>
        /// <param name="websiteSiteNameFunctionId">Unique identifier for the user function based on the WEBSITE_SITE_NAME configuration value</param>
        /// <param name="hostIdFunctionId">Unique identifier for the user function based on the hostId for the function</param>
        /// <param name="executor">Defines contract for triggering user function</param>
        /// <param name="sqlOptions"></param>
        /// <param name="logger">Facilitates logging of messages</param>
        /// <param name="configuration">Provides configuration values</param>
        public SqlTriggerListener(string connectionString, string tableName, string userDefinedLeasesTableName, string websiteSiteNameFunctionId, string hostIdFunctionId, ITriggeredFunctionExecutor executor, SqlOptions sqlOptions, ILogger logger, IConfiguration configuration)
        {
            this._connectionString = !string.IsNullOrEmpty(connectionString) ? connectionString : throw new ArgumentNullException(nameof(connectionString));
            this._userTable = !string.IsNullOrEmpty(tableName) ? new SqlObject(tableName) : throw new ArgumentNullException(nameof(tableName));
            this._userDefinedLeasesTableName = userDefinedLeasesTableName;
            // We'll use the WEBSITE_SITE_NAME based ID if we have it, but some environments (like running locally) may not have it
            // so we'll just fall back to the host ID version instead
            this._userFunctionId = string.IsNullOrEmpty(websiteSiteNameFunctionId) ? hostIdFunctionId : websiteSiteNameFunctionId;
            this._hostIdFunctionId = hostIdFunctionId;
            this._executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this._sqlOptions = sqlOptions ?? throw new ArgumentNullException(nameof(sqlOptions));
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            int? configuredMaxChangesPerWorker;
            // TODO: when we move to reading them exclusively from the host options, remove reading from settings.(https://github.com/Azure/azure-functions-sql-extension/issues/961)
            configuredMaxChangesPerWorker = configuration.GetValue<int?>(ConfigKey_SqlTrigger_MaxChangesPerWorker);
            this._maxChangesPerWorker = configuredMaxChangesPerWorker ?? this._sqlOptions.MaxChangesPerWorker;
            if (this._maxChangesPerWorker <= 0)
            {
                throw new InvalidOperationException($"Invalid value for configuration setting '{ConfigKey_SqlTrigger_MaxChangesPerWorker}'. Ensure that the value is a positive integer.");
            }
            this._hasConfiguredMaxChangesPerWorker = configuredMaxChangesPerWorker != null;

            this._scaleMonitor = new SqlTriggerScaleMonitor(this._userFunctionId, this._userTable, this._userDefinedLeasesTableName, this._connectionString, this._maxChangesPerWorker, this._logger);
            this._targetScaler = new SqlTriggerTargetScaler(this._userFunctionId, this._userTable, this._userDefinedLeasesTableName, this._connectionString, this._maxChangesPerWorker, this._logger);
        }

        public void Cancel()
        {
            this.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            // Nothing to dispose.
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            int previousState = Interlocked.CompareExchange(ref this._listenerState, ListenerStarting, ListenerNotStarted);

            switch (previousState)
            {
                case ListenerStarting: throw new InvalidOperationException("The listener is already starting.");
                case ListenerStarted: throw new InvalidOperationException("The listener has already started.");
                default: break;
            }

            this.InitializeTelemetryProps();

            try
            {
                using (var connection = new SqlConnection(this._connectionString))
                {
                    await connection.OpenAsyncWithSqlErrorHandling(cancellationToken);
                    ServerProperties serverProperties = await GetServerTelemetryProperties(connection, this._logger, cancellationToken);
                    this._telemetryProps.AddConnectionProps(connection, serverProperties);

                    await VerifyDatabaseSupported(connection, this._logger, cancellationToken);

                    int userTableId = await GetUserTableIdAsync(connection, this._userTable, this._logger, cancellationToken);
                    IReadOnlyList<(string name, string type)> primaryKeyColumns = GetPrimaryKeyColumns(connection, userTableId, this._logger, this._userTable.FullName, cancellationToken);
                    IReadOnlyList<string> userTableColumns = this.GetUserTableColumns(connection, userTableId, cancellationToken);

                    string bracketedLeasesTableName = GetBracketedLeasesTableName(this._userDefinedLeasesTableName, this._userFunctionId, userTableId);
                    this._telemetryProps[TelemetryPropertyName.LeasesTableName] = bracketedLeasesTableName;

                    var transactionSw = Stopwatch.StartNew();
                    long createdSchemaDurationMs = 0L, createGlobalStateTableDurationMs = 0L, insertGlobalStateTableRowDurationMs = 0L, createLeasesTableDurationMs = 0L;
                    using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
                    {
                        createdSchemaDurationMs = await this.CreateSchemaAsync(connection, transaction, cancellationToken);
                        createGlobalStateTableDurationMs = await this.CreateGlobalStateTableAsync(connection, transaction, cancellationToken);
                        insertGlobalStateTableRowDurationMs = await this.InsertGlobalStateTableRowAsync(connection, transaction, userTableId, cancellationToken);
                        createLeasesTableDurationMs = await this.CreateLeasesTableAsync(connection, transaction, bracketedLeasesTableName, primaryKeyColumns, cancellationToken);
                        transaction.Commit();
                    }

                    this._changeMonitor = new SqlTableChangeMonitor<T>(
                        this._connectionString,
                        userTableId,
                        this._userTable,
                        this._userFunctionId,
                        bracketedLeasesTableName,
                        userTableColumns,
                        primaryKeyColumns,
                        this._executor,
                        this._sqlOptions,
                        this._logger,
                        this._configuration,
                        this._telemetryProps);

                    this._listenerState = ListenerStarted;
                    this._logger.LogDebug($"Started SQL trigger listener for table: '{this._userTable.FullName}' (object ID: {userTableId}), function ID: {this._userFunctionId}, leases table: {bracketedLeasesTableName}");

                    var measures = new Dictionary<TelemetryMeasureName, double>
                    {
                        [TelemetryMeasureName.CreatedSchemaDurationMs] = createdSchemaDurationMs,
                        [TelemetryMeasureName.CreateGlobalStateTableDurationMs] = createGlobalStateTableDurationMs,
                        [TelemetryMeasureName.InsertGlobalStateTableRowDurationMs] = insertGlobalStateTableRowDurationMs,
                        [TelemetryMeasureName.CreateLeasesTableDurationMs] = createLeasesTableDurationMs,
                        [TelemetryMeasureName.TransactionDurationMs] = transactionSw.ElapsedMilliseconds,
                        [TelemetryMeasureName.MaxChangesPerWorker] = this._maxChangesPerWorker
                    };

                    TelemetryInstance.TrackEvent(
                        TelemetryEventName.StartListener,
                        new Dictionary<TelemetryPropertyName, string>(this._telemetryProps) {
                            { TelemetryPropertyName.HasConfiguredMaxChangesPerWorker, this._hasConfiguredMaxChangesPerWorker.ToString() }
                        },
                        measures);
                }
            }
            catch (Exception ex)
            {
                this._listenerState = ListenerNotStarted;
                this._logger.LogError($"Failed to start SQL trigger listener for table: '{this._userTable.FullName}', function ID: '{this._userFunctionId}'. Exception: {ex}");
                TelemetryInstance.TrackException(TelemetryErrorName.StartListener, ex, this._telemetryProps);

                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            int previousState = Interlocked.CompareExchange(ref this._listenerState, ListenerStopping, ListenerStarted);
            if (previousState == ListenerStarted)
            {
                this._changeMonitor.Dispose();

                this._listenerState = ListenerStopped;
            }

            var measures = new Dictionary<TelemetryMeasureName, double>
            {
                [TelemetryMeasureName.DurationMs] = stopwatch.ElapsedMilliseconds,
            };

            TelemetryInstance.TrackEvent(TelemetryEventName.StopListener, this._telemetryProps, measures);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the column names of the user table.
        /// </summary>
        private List<string> GetUserTableColumns(SqlConnection connection, int userTableId, CancellationToken cancellationToken)
        {
            const int NameIndex = 0, TypeIndex = 1, IsAssemblyTypeIndex = 2;
            string getUserTableColumnsQuery = $@"
                SELECT
                    c.name,
                    t.name,
                    t.is_assembly_type
                FROM sys.columns AS c
                INNER JOIN sys.types AS t ON c.user_type_id = t.user_type_id
                WHERE c.object_id = {userTableId};
            ";

            using (var getUserTableColumnsCommand = new SqlCommand(getUserTableColumnsQuery, connection))
            using (SqlDataReader reader = getUserTableColumnsCommand.ExecuteReaderWithLogging(this._logger))
            {
                var userTableColumns = new List<string>();
                var userDefinedTypeColumns = new List<(string name, string type)>();

                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string columnName = reader.GetString(NameIndex);
                    string columnType = reader.GetString(TypeIndex);
                    bool isAssemblyType = reader.GetBoolean(IsAssemblyTypeIndex);

                    userTableColumns.Add(columnName);

                    if (isAssemblyType)
                    {
                        userDefinedTypeColumns.Add((columnName, columnType));
                    }
                }

                if (userDefinedTypeColumns.Count > 0)
                {
                    string columnNamesAndTypes = string.Join(", ", userDefinedTypeColumns.Select(col => $"'{col.name}' (type: {col.type})"));
                    throw new InvalidOperationException($"Found column(s) with unsupported type(s): {columnNamesAndTypes} in table: '{this._userTable.FullName}'.");
                }

                var conflictingColumnNames = userTableColumns.Intersect(ReservedColumnNames).ToList();

                if (conflictingColumnNames.Count > 0)
                {
                    string columnNames = string.Join(", ", conflictingColumnNames.Select(col => $"'{col}'"));
                    throw new InvalidOperationException($"Found reserved column name(s): {columnNames} in table: '{this._userTable.FullName}'." +
                        " Please rename them to be able to use trigger binding.");
                }

                this._logger.LogDebug($"GetUserTableColumns ColumnNames = {string.Join(", ", userTableColumns.Select(col => $"'{col}'"))}.");
                return userTableColumns;
            }
        }

        /// <summary>
        /// Creates the schema for global state table and leases tables, if it does not already exist.
        /// </summary>
        /// <param name="connection">The already-opened connection to use for executing the command</param>
        /// <param name="transaction">The transaction wrapping this command</param>
        /// <param name="cancellationToken">Cancellation token to pass to the command</param>
        /// <returns>The time taken in ms to execute the command</returns>
        private async Task<long> CreateSchemaAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            string createSchemaQuery = $@"
                {AppLockStatements}

                IF SCHEMA_ID(N'{SchemaName}') IS NULL
                    EXEC ('CREATE SCHEMA {SchemaName}');
            ";

            using (var createSchemaCommand = new SqlCommand(createSchemaQuery, connection, transaction))
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    await createSchemaCommand.ExecuteNonQueryAsyncWithLogging(this._logger, cancellationToken);
                }
                catch (Exception ex)
                {
                    TelemetryInstance.TrackException(TelemetryErrorName.CreateSchema, ex, this._telemetryProps);
                    var sqlEx = ex as SqlException;
                    if (sqlEx?.Number == ObjectAlreadyExistsErrorNumber)
                    {
                        // This generally shouldn't happen since we check for its existence in the statement but occasionally
                        // a race condition can make it so that multiple instances will try and create the schema at once.
                        // In that case we can just ignore the error since all we care about is that the schema exists at all.
                        this._logger.LogWarning($"Failed to create schema '{SchemaName}'. Exception message: {ex.Message} This is informational only, function startup will continue as normal.");
                    }
                    else
                    {
                        throw;
                    }
                }

                return stopwatch.ElapsedMilliseconds;
            }
        }

        /// <summary>
        /// Creates the global state table if it does not already exist.
        /// </summary>
        /// <param name="connection">The already-opened connection to use for executing the command</param>
        /// <param name="transaction">The transaction wrapping this command</param>
        /// <param name="cancellationToken">Cancellation token to pass to the command</param>
        /// <returns>The time taken in ms to execute the command</returns>
        private async Task<long> CreateGlobalStateTableAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            string createGlobalStateTableQuery = $@"
                {AppLockStatements}

                IF OBJECT_ID(N'{GlobalStateTableName}', 'U') IS NULL
                    CREATE TABLE {GlobalStateTableName} (
                        UserFunctionID char(16) NOT NULL,
                        UserTableID int NOT NULL,
                        LastSyncVersion bigint NOT NULL,
                        LastAccessTime Datetime NOT NULL DEFAULT GETUTCDATE(),
                        PRIMARY KEY (UserFunctionID, UserTableID)
                    );
                ELSE IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE Name = N'LastAccessTime'
                    AND Object_ID = Object_ID(N'{GlobalStateTableName}'))
                        ALTER TABLE {GlobalStateTableName} ADD LastAccessTime Datetime NOT NULL DEFAULT GETUTCDATE();
            ";

            using (var createGlobalStateTableCommand = new SqlCommand(createGlobalStateTableQuery, connection, transaction))
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    await createGlobalStateTableCommand.ExecuteNonQueryAsyncWithLogging(this._logger, cancellationToken);
                }
                catch (Exception ex)
                {
                    TelemetryInstance.TrackException(TelemetryErrorName.CreateGlobalStateTable, ex, this._telemetryProps);
                    var sqlEx = ex as SqlException;
                    if (sqlEx?.Number == ObjectAlreadyExistsErrorNumber)
                    {
                        // This generally shouldn't happen since we check for its existence in the statement but occasionally
                        // a race condition can make it so that multiple instances will try and create the schema at once.
                        // In that case we can just ignore the error since all we care about is that the schema exists at all.
                        this._logger.LogWarning($"Failed to create global state table '{GlobalStateTableName}'. Exception message: {ex.Message} This is informational only, function startup will continue as normal.");
                    }
                    else
                    {
                        throw;
                    }
                }
                return stopwatch.ElapsedMilliseconds;
            }
        }

        /// <summary>
        /// Inserts row for the 'user function and table' inside the global state table, if one does not already exist.
        /// </summary>
        /// <param name="connection">The already-opened connection to use for executing the command</param>
        /// <param name="transaction">The transaction wrapping this command</param>
        /// <param name="userTableId">The ID of the table being watched</param>
        /// <param name="cancellationToken">Cancellation token to pass to the command</param>
        /// <returns>The time taken in ms to execute the command</returns>
        private async Task<long> InsertGlobalStateTableRowAsync(SqlConnection connection, SqlTransaction transaction, int userTableId, CancellationToken cancellationToken)
        {
            object minValidVersion;

            string getMinValidVersionQuery = $"SELECT CHANGE_TRACKING_MIN_VALID_VERSION({userTableId});";

            using (var getMinValidVersionCommand = new SqlCommand(getMinValidVersionQuery, connection, transaction))
            using (SqlDataReader reader = getMinValidVersionCommand.ExecuteReaderWithLogging(this._logger))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException($"Received empty response when querying the 'change tracking min valid version' for table: '{this._userTable.FullName}'.");
                }

                minValidVersion = reader.GetValue(0);

                if (minValidVersion is DBNull)
                {
                    throw new InvalidOperationException($"Could not find change tracking enabled for table: '{this._userTable.FullName}'.");
                }
            }

            string insertRowGlobalStateTableQuery = $@"
                {AppLockStatements}
                -- For back compatibility copy the lastSyncVersion from _hostIdFunctionId if it exists.
                IF NOT EXISTS (
                    SELECT * FROM {GlobalStateTableName}
                    WHERE UserFunctionID = '{this._userFunctionId}' AND UserTableID = {userTableId}
                )
                BEGIN
                    -- Migrate LastSyncVersion from oldUserFunctionId if it exists and delete the record
                    DECLARE @lastSyncVersion bigint;
                    SELECT @lastSyncVersion = LastSyncVersion from az_func.GlobalState where UserFunctionID = '{this._hostIdFunctionId}' AND UserTableID = {userTableId}
                    IF @lastSyncVersion IS NULL
                        SET @lastSyncVersion = {(long)minValidVersion};
                    ELSE
                        DELETE FROM az_func.GlobalState WHERE UserFunctionID = '{this._hostIdFunctionId}' AND UserTableID = {userTableId}

                    INSERT INTO {GlobalStateTableName}
                    VALUES ('{this._userFunctionId}', {userTableId}, @lastSyncVersion, GETUTCDATE());
                END
            ";

            using (var insertRowGlobalStateTableCommand = new SqlCommand(insertRowGlobalStateTableQuery, connection, transaction))
            {
                var stopwatch = Stopwatch.StartNew();
                int rowsInserted = await insertRowGlobalStateTableCommand.ExecuteNonQueryAsyncWithLogging(this._logger, cancellationToken);
                if (rowsInserted > 0)
                {
                    TelemetryInstance.TrackEvent(TelemetryEventName.InsertGlobalStateTableRow);
                }
                return stopwatch.ElapsedMilliseconds;
            }
        }

        /// <summary>
        /// Creates the leases table for the 'user function and table', if one does not already exist.
        /// </summary>
        /// <param name="connection">The already-opened connection to use for executing the command</param>
        /// <param name="transaction">The transaction wrapping this command</param>
        /// <param name="leasesTableName">The name of the leases table to create</param>
        /// <param name="primaryKeyColumns">The primary keys of the user table this leases table is for</param>
        /// <param name="cancellationToken">Cancellation token to pass to the command</param>
        /// <returns>The time taken in ms to execute the command</returns>
        private async Task<long> CreateLeasesTableAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string leasesTableName,
            IReadOnlyList<(string name, string type)> primaryKeyColumns,
            CancellationToken cancellationToken)
        {
            string primaryKeysWithTypes = string.Join(", ", primaryKeyColumns.Select(col => $"{col.name.AsBracketQuotedString()} {col.type}"));
            string primaryKeys = string.Join(", ", primaryKeyColumns.Select(col => col.name.AsBracketQuotedString()));
            string oldLeasesTableName = leasesTableName.Contains(this._userFunctionId) ? leasesTableName.Replace(this._userFunctionId, this._hostIdFunctionId) : string.Empty;
            // We should only migrate the lease table from the old hostId based one to the newer WEBSITE_SITE_NAME one if
            // we're actually using the WEBSITE_SITE_NAME one (e.g. leasesTableName is different)
            bool shouldMigrateOldLeasesTable = !string.IsNullOrEmpty(oldLeasesTableName) && oldLeasesTableName != leasesTableName;
            string createLeasesTableQuery = shouldMigrateOldLeasesTable ? $@"
                {AppLockStatements}

                IF OBJECT_ID(N'{leasesTableName}', 'U') IS NULL
                BEGIN
                    CREATE TABLE {leasesTableName} (
                        {primaryKeysWithTypes},
                        {LeasesTableChangeVersionColumnName} bigint NOT NULL,
                        {LeasesTableAttemptCountColumnName} int NOT NULL,
                        {LeasesTableLeaseExpirationTimeColumnName} datetime2,
                        PRIMARY KEY ({primaryKeys})
                    );

                    -- Migrate all data from OldLeasesTable and delete it.
                    IF OBJECT_ID(N'{oldLeasesTableName}', 'U') IS NOT NULL
                    BEGIN
                        INSERT INTO {leasesTableName}
                        SELECT * FROM {oldLeasesTableName};

                        DROP TABLE {oldLeasesTableName};
                    END
                End
            " :
            $@"
                {AppLockStatements}

                IF OBJECT_ID(N'{leasesTableName}', 'U') IS NULL
                    CREATE TABLE {leasesTableName} (
                        {primaryKeysWithTypes},
                        {LeasesTableChangeVersionColumnName} bigint NOT NULL,
                        {LeasesTableAttemptCountColumnName} int NOT NULL,
                        {LeasesTableLeaseExpirationTimeColumnName} datetime2,
                        PRIMARY KEY ({primaryKeys})
                    );
            ";

            using (var createLeasesTableCommand = new SqlCommand(createLeasesTableQuery, connection, transaction))
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    await createLeasesTableCommand.ExecuteNonQueryAsyncWithLogging(this._logger, cancellationToken, true);
                }
                catch (Exception ex)
                {
                    TelemetryInstance.TrackException(TelemetryErrorName.CreateLeasesTable, ex, this._telemetryProps);
                    var sqlEx = ex as SqlException;
                    if (sqlEx?.Number == ObjectAlreadyExistsErrorNumber)
                    {
                        // This generally shouldn't happen since we check for its existence in the statement but occasionally
                        // a race condition can make it so that multiple instances will try and create the schema at once.
                        // In that case we can just ignore the error since all we care about is that the schema exists at all.
                        this._logger.LogWarning($"Failed to create leases table '{leasesTableName}'. Exception message: {ex.Message} This is informational only, function startup will continue as normal.");
                    }
                    else
                    {
                        throw;
                    }
                }
                long durationMs = stopwatch.ElapsedMilliseconds;
                return durationMs;
            }
        }
        public IScaleMonitor GetMonitor()
        {
            return this._scaleMonitor;
        }

        public ITargetScaler GetTargetScaler()
        {
            return this._targetScaler;
        }

        /// <summary>
        /// Clears the current telemetry property dictionary and initializes the default initial properties.
        /// </summary>
        private void InitializeTelemetryProps()
        {
            this._telemetryProps.Clear();
            this._telemetryProps[TelemetryPropertyName.UserFunctionId] = this._userFunctionId;
        }
    }
}