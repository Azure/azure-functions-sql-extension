// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry;
using static Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry.Telemetry;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlTriggerConstants;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlBindingUtilities;
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

        // NOTE: please ensure the Readme file and other public documentation are also updated if this value ever
        // needs to be changed.
        public const int DefaultMaxChangesPerWorker = 1000;

        private readonly SqlObject _userTable;
        private readonly string _connectionString;
        private readonly string _userFunctionId;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        private readonly IDictionary<TelemetryPropertyName, string> _telemetryProps = new Dictionary<TelemetryPropertyName, string>();
        private readonly int _maxChangesPerWorker;
        private readonly bool _hasConfiguredMaxChangesPerWorker = false;

        private SqlTableChangeMonitor<T> _changeMonitor;
        private IScaleMonitor<SqlTriggerMetrics> _scaleMonitor;
        private ITargetScaler _targetScaler;

        private int _listenerState = ListenerNotStarted;


        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerListener{T}"/> class.
        /// </summary>
        /// <param name="connectionString">SQL connection string used to connect to user database</param>
        /// <param name="tableName">Name of the user table</param>
        /// <param name="userFunctionId">Unique identifier for the user function</param>
        /// <param name="executor">Defines contract for triggering user function</param>
        /// <param name="logger">Facilitates logging of messages</param>
        /// <param name="configuration">Provides configuration values</param>
        public SqlTriggerListener(string connectionString, string tableName, string userFunctionId, ITriggeredFunctionExecutor executor, ILogger logger, IConfiguration configuration)
        {
            this._connectionString = !string.IsNullOrEmpty(connectionString) ? connectionString : throw new ArgumentNullException(nameof(connectionString));
            this._userTable = !string.IsNullOrEmpty(tableName) ? new SqlObject(tableName) : throw new ArgumentNullException(nameof(tableName));
            this._userFunctionId = !string.IsNullOrEmpty(userFunctionId) ? userFunctionId : throw new ArgumentNullException(nameof(userFunctionId));
            this._executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            int? configuredMaxChangesPerWorker;
            configuredMaxChangesPerWorker = configuration.GetValue<int?>(ConfigKey_SqlTrigger_MaxChangesPerWorker);
            this._maxChangesPerWorker = configuredMaxChangesPerWorker ?? DefaultMaxChangesPerWorker;
            if (this._maxChangesPerWorker <= 0)
            {
                throw new InvalidOperationException($"Invalid value for configuration setting '{ConfigKey_SqlTrigger_MaxChangesPerWorker}'. Ensure that the value is a positive integer.");
            }
            this._hasConfiguredMaxChangesPerWorker = configuredMaxChangesPerWorker != null;
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
            TelemetryInstance.TrackEvent(
                TelemetryEventName.StartListenerStart,
                new Dictionary<TelemetryPropertyName, string>(this._telemetryProps) {
                        { TelemetryPropertyName.HasConfiguredMaxChangesPerWorker, this._hasConfiguredMaxChangesPerWorker.ToString() }
                },
                new Dictionary<TelemetryMeasureName, double>() {
                    { TelemetryMeasureName.MaxChangesPerWorker, this._maxChangesPerWorker }
                }
            );

            try
            {
                using (var connection = new SqlConnection(this._connectionString))
                {
                    this._logger.LogDebugWithThreadId("BEGIN OpenListenerConnection");
                    await connection.OpenAsync(cancellationToken);
                    this._logger.LogDebugWithThreadId("END OpenListenerConnection");
                    this._telemetryProps.AddConnectionProps(connection);

                    await VerifyDatabaseSupported(connection, this._logger, cancellationToken);

                    int userTableId = await this.GetUserTableIdAsync(connection, cancellationToken);
                    IReadOnlyList<(string name, string type)> primaryKeyColumns = await this.GetPrimaryKeyColumnsAsync(connection, userTableId, cancellationToken);
                    IReadOnlyList<string> userTableColumns = await this.GetUserTableColumnsAsync(connection, userTableId, cancellationToken);

                    string leasesTableName = string.Format(CultureInfo.InvariantCulture, LeasesTableNameFormat, $"{this._userFunctionId}_{userTableId}");
                    this._telemetryProps[TelemetryPropertyName.LeasesTableName] = leasesTableName;

                    var transactionSw = Stopwatch.StartNew();
                    long createdSchemaDurationMs = 0L, createGlobalStateTableDurationMs = 0L, insertGlobalStateTableRowDurationMs = 0L, createLeasesTableDurationMs = 0L;

                    using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
                    {
                        createdSchemaDurationMs = await this.CreateSchemaAsync(connection, transaction, cancellationToken);
                        createGlobalStateTableDurationMs = await this.CreateGlobalStateTableAsync(connection, transaction, cancellationToken);
                        insertGlobalStateTableRowDurationMs = await this.InsertGlobalStateTableRowAsync(connection, transaction, userTableId, cancellationToken);
                        createLeasesTableDurationMs = await this.CreateLeasesTableAsync(connection, transaction, leasesTableName, primaryKeyColumns, cancellationToken);
                        transaction.Commit();
                    }

                    this._logger.LogInformation($"Starting SQL trigger listener for table: '{this._userTable.FullName}', function ID: '{this._userFunctionId}'.");

                    this._changeMonitor = new SqlTableChangeMonitor<T>(
                        this._connectionString,
                        userTableId,
                        this._userTable,
                        this._userFunctionId,
                        leasesTableName,
                        userTableColumns,
                        primaryKeyColumns,
                        this._executor,
                        this._logger,
                        this._configuration,
                        this._telemetryProps);

                    this._listenerState = ListenerStarted;
                    this._logger.LogInformation($"Started SQL trigger listener for table: '{this._userTable.FullName}', function ID: '{this._userFunctionId}'.");

                    var measures = new Dictionary<TelemetryMeasureName, double>
                    {
                        [TelemetryMeasureName.CreatedSchemaDurationMs] = createdSchemaDurationMs,
                        [TelemetryMeasureName.CreateGlobalStateTableDurationMs] = createGlobalStateTableDurationMs,
                        [TelemetryMeasureName.InsertGlobalStateTableRowDurationMs] = insertGlobalStateTableRowDurationMs,
                        [TelemetryMeasureName.CreateLeasesTableDurationMs] = createLeasesTableDurationMs,
                        [TelemetryMeasureName.TransactionDurationMs] = transactionSw.ElapsedMilliseconds,
                    };

                    TelemetryInstance.TrackEvent(TelemetryEventName.StartListenerEnd, this._telemetryProps, measures);

                    this._scaleMonitor = new SqlTriggerScaleMonitor<T>(this._userFunctionId, this._userTable, this._changeMonitor, this._maxChangesPerWorker, this._logger);
                    this._targetScaler = new SqlTriggerTargetScaler<T>(this._userFunctionId, this._logger, this._maxChangesPerWorker, this._changeMonitor);
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
            TelemetryInstance.TrackEvent(TelemetryEventName.StopListenerStart, this._telemetryProps);
            var stopwatch = Stopwatch.StartNew();

            int previousState = Interlocked.CompareExchange(ref this._listenerState, ListenerStopping, ListenerStarted);
            if (previousState == ListenerStarted)
            {
                this._changeMonitor.Dispose();

                this._listenerState = ListenerStopped;
                this._logger.LogInformation($"Stopped SQL trigger listener for table: '{this._userTable.FullName}', function ID: '{this._userFunctionId}'.");
            }

            var measures = new Dictionary<TelemetryMeasureName, double>
            {
                [TelemetryMeasureName.DurationMs] = stopwatch.ElapsedMilliseconds,
            };

            TelemetryInstance.TrackEvent(TelemetryEventName.StopListenerEnd, this._telemetryProps, measures);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Returns the object ID of the user table.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown in case of error when querying the object ID for the user table</exception>
        private async Task<int> GetUserTableIdAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            string getObjectIdQuery = $"SELECT OBJECT_ID(N{this._userTable.QuotedFullName}, 'U');";

            this._logger.LogDebugWithThreadId($"BEGIN GetUserTableId Query={getObjectIdQuery}");
            using (var getObjectIdCommand = new SqlCommand(getObjectIdQuery, connection))
            using (SqlDataReader reader = await getObjectIdCommand.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException($"Received empty response when querying the object ID for table: '{this._userTable.FullName}'.");
                }

                object userTableId = reader.GetValue(0);

                if (userTableId is DBNull)
                {
                    throw new InvalidOperationException($"Could not find table: '{this._userTable.FullName}'.");
                }
                this._logger.LogDebugWithThreadId($"END GetUserTableId TableId={userTableId}");
                return (int)userTableId;
            }
        }

        /// <summary>
        /// Gets the names and types of primary key columns of the user table.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if there are no primary key columns present in the user table or if their names conflict with columns in leases table.
        /// </exception>
        private async Task<IReadOnlyList<(string name, string type)>> GetPrimaryKeyColumnsAsync(SqlConnection connection, int userTableId, CancellationToken cancellationToken)
        {
            const int NameIndex = 0, TypeIndex = 1, LengthIndex = 2, PrecisionIndex = 3, ScaleIndex = 4;
            string getPrimaryKeyColumnsQuery = $@"
                SELECT 
                    c.name, 
                    t.name, 
                    c.max_length, 
                    c.precision, 
                    c.scale
                FROM sys.indexes AS i
                INNER JOIN sys.index_columns AS ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns AS c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                INNER JOIN sys.types AS t ON c.user_type_id = t.user_type_id
                WHERE i.is_primary_key = 1 AND i.object_id = {userTableId};
            ";
            this._logger.LogDebugWithThreadId($"BEGIN GetPrimaryKeyColumns Query={getPrimaryKeyColumnsQuery}");
            using (var getPrimaryKeyColumnsCommand = new SqlCommand(getPrimaryKeyColumnsQuery, connection))
            using (SqlDataReader reader = await getPrimaryKeyColumnsCommand.ExecuteReaderAsync(cancellationToken))
            {
                string[] variableLengthTypes = new[] { "varchar", "nvarchar", "nchar", "char", "binary", "varbinary" };
                string[] variablePrecisionTypes = new[] { "numeric", "decimal" };

                var primaryKeyColumns = new List<(string name, string type)>();

                while (await reader.ReadAsync(cancellationToken))
                {
                    string name = reader.GetString(NameIndex);
                    string type = reader.GetString(TypeIndex);

                    if (variableLengthTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                    {
                        // Special "max" case. I'm actually not sure it's valid to have varchar(max) as a primary key because
                        // it exceeds the byte limit of an index field (900 bytes), but just in case
                        short length = reader.GetInt16(LengthIndex);
                        type += length == -1 ? "(max)" : $"({length})";
                    }
                    else if (variablePrecisionTypes.Contains(type))
                    {
                        byte precision = reader.GetByte(PrecisionIndex);
                        byte scale = reader.GetByte(ScaleIndex);
                        type += $"({precision},{scale})";
                    }

                    primaryKeyColumns.Add((name, type));
                }

                if (primaryKeyColumns.Count == 0)
                {
                    throw new InvalidOperationException($"Could not find primary key created in table: '{this._userTable.FullName}'.");
                }

                this._logger.LogDebugWithThreadId($"END GetPrimaryKeyColumns ColumnNames(types) = {string.Join(", ", primaryKeyColumns.Select(col => $"'{col.name}({col.type})'"))}.");
                return primaryKeyColumns;
            }
        }

        /// <summary>
        /// Gets the column names of the user table.
        /// </summary>
        private async Task<IReadOnlyList<string>> GetUserTableColumnsAsync(SqlConnection connection, int userTableId, CancellationToken cancellationToken)
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

            this._logger.LogDebugWithThreadId($"BEGIN GetUserTableColumns Query={getUserTableColumnsQuery}");
            using (var getUserTableColumnsCommand = new SqlCommand(getUserTableColumnsQuery, connection))
            using (SqlDataReader reader = await getUserTableColumnsCommand.ExecuteReaderAsync(cancellationToken))
            {
                var userTableColumns = new List<string>();
                var userDefinedTypeColumns = new List<(string name, string type)>();

                while (await reader.ReadAsync(cancellationToken))
                {
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

                this._logger.LogDebugWithThreadId($"END GetUserTableColumns ColumnNames = {string.Join(", ", userTableColumns.Select(col => $"'{col}'"))}.");
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

            this._logger.LogDebugWithThreadId($"BEGIN CreateSchema Query={createSchemaQuery}");
            using (var createSchemaCommand = new SqlCommand(createSchemaQuery, connection, transaction))
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    await createSchemaCommand.ExecuteNonQueryAsync(cancellationToken);
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

                long durationMs = stopwatch.ElapsedMilliseconds;
                this._logger.LogDebugWithThreadId($"END CreateSchema Duration={durationMs}ms");
                return durationMs;
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
                        PRIMARY KEY (UserFunctionID, UserTableID)
                    );
            ";

            this._logger.LogDebugWithThreadId($"BEGIN CreateGlobalStateTable Query={createGlobalStateTableQuery}");
            using (var createGlobalStateTableCommand = new SqlCommand(createGlobalStateTableQuery, connection, transaction))
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    await createGlobalStateTableCommand.ExecuteNonQueryAsync(cancellationToken);
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
                long durationMs = stopwatch.ElapsedMilliseconds;
                this._logger.LogDebugWithThreadId($"END CreateGlobalStateTable Duration={durationMs}ms");
                return durationMs;
            }
        }

        /// <summary>
        /// Inserts row for the 'user function and table' inside the global state table, if one does not already exist.
        /// </summary>
        /// <param name="connection">The already-opened connection to use for executing the command</param>
        /// <param name="transaction">The transaction wrapping this command</param>
        /// <param name="cancellationToken">Cancellation token to pass to the command</param>
        /// <returns>The time taken in ms to execute the command</returns>
        private async Task<long> InsertGlobalStateTableRowAsync(SqlConnection connection, SqlTransaction transaction, int userTableId, CancellationToken cancellationToken)
        {
            object minValidVersion;

            string getMinValidVersionQuery = $"SELECT CHANGE_TRACKING_MIN_VALID_VERSION({userTableId});";

            this._logger.LogDebugWithThreadId($"BEGIN InsertGlobalStateTableRow");
            this._logger.LogDebugWithThreadId($"BEGIN GetMinValidVersion Query={getMinValidVersionQuery}");
            using (var getMinValidVersionCommand = new SqlCommand(getMinValidVersionQuery, connection, transaction))
            using (SqlDataReader reader = await getMinValidVersionCommand.ExecuteReaderAsync(cancellationToken))
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
            this._logger.LogDebugWithThreadId($"END GetMinValidVersion MinValidVersion={minValidVersion}");

            string insertRowGlobalStateTableQuery = $@"
                {AppLockStatements}

                IF NOT EXISTS (
                    SELECT * FROM {GlobalStateTableName}
                    WHERE UserFunctionID = '{this._userFunctionId}' AND UserTableID = {userTableId}
                )
                    INSERT INTO {GlobalStateTableName}
                    VALUES ('{this._userFunctionId}', {userTableId}, {(long)minValidVersion});
            ";

            this._logger.LogDebugWithThreadId($"BEGIN InsertRowGlobalStateTableQuery Query={insertRowGlobalStateTableQuery}");
            using (var insertRowGlobalStateTableCommand = new SqlCommand(insertRowGlobalStateTableQuery, connection, transaction))
            {
                var stopwatch = Stopwatch.StartNew();
                await insertRowGlobalStateTableCommand.ExecuteNonQueryAsync(cancellationToken);
                long durationMs = stopwatch.ElapsedMilliseconds;
                this._logger.LogDebugWithThreadId($"END InsertRowGlobalStateTableQuery Duration={durationMs}ms");
                this._logger.LogDebugWithThreadId("END InsertGlobalStateTableRow");
                return durationMs;
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

            string createLeasesTableQuery = $@"
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

            this._logger.LogDebugWithThreadId($"BEGIN CreateLeasesTable Query={createLeasesTableQuery}");
            using (var createLeasesTableCommand = new SqlCommand(createLeasesTableQuery, connection, transaction))
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    await createLeasesTableCommand.ExecuteNonQueryAsync(cancellationToken);
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
                        this._logger.LogWarning($"Failed to create global state table '{leasesTableName}'. Exception message: {ex.Message} This is informational only, function startup will continue as normal.");
                    }
                    else
                    {
                        throw;
                    }
                }
                long durationMs = stopwatch.ElapsedMilliseconds;
                this._logger.LogDebugWithThreadId($"END CreateLeasesTable Duration={durationMs}ms");
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