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
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MoreLinq;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Represents the listener to SQL table changes.
    /// </summary>
    /// <typeparam name="T">POCO class representing the row in the user table</typeparam>
    internal sealed class SqlTriggerListener<T> : IListener, IScaleMonitor<SqlTriggerMetrics>
    {
        private const int ListenerNotStarted = 0;
        private const int ListenerStarting = 1;
        private const int ListenerStarted = 2;
        private const int ListenerStopping = 3;
        private const int ListenerStopped = 4;

        private readonly SqlObject _userTable;
        private readonly string _connectionString;
        private readonly string _userFunctionId;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly ScaleMonitorDescriptor _scaleMonitorDescriptor;

        private readonly IDictionary<TelemetryPropertyName, string> _telemetryProps = new Dictionary<TelemetryPropertyName, string>();

        private SqlTableChangeMonitor<T> _changeMonitor;
        private int _listenerState = ListenerNotStarted;

        ScaleMonitorDescriptor IScaleMonitor.Descriptor => this._scaleMonitorDescriptor;

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

            // Do not convert the scale-monitor ID to lower-case string since SQL table names can be case-sensitive
            // depending on the collation of the current database.
            this._scaleMonitorDescriptor = new ScaleMonitorDescriptor($"{userFunctionId}-SqlTrigger-{tableName}");
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
            TelemetryInstance.TrackEvent(TelemetryEventName.StartListenerStart, this._telemetryProps);

            try
            {
                using (var connection = new SqlConnection(this._connectionString))
                {
                    this._logger.LogDebugWithThreadId("BEGIN OpenListenerConnection");
                    await connection.OpenAsync(cancellationToken);
                    this._logger.LogDebugWithThreadId("END OpenListenerConnection");
                    this._telemetryProps.AddConnectionProps(connection);

                    int userTableId = await this.GetUserTableIdAsync(connection, cancellationToken);
                    IReadOnlyList<(string name, string type)> primaryKeyColumns = await this.GetPrimaryKeyColumnsAsync(connection, userTableId, cancellationToken);
                    IReadOnlyList<string> userTableColumns = await this.GetUserTableColumnsAsync(connection, userTableId, cancellationToken);

                    string leasesTableName = string.Format(CultureInfo.InvariantCulture, SqlTriggerConstants.LeasesTableNameFormat, $"{this._userFunctionId}_{userTableId}");
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
            string getPrimaryKeyColumnsQuery = $@"
                SELECT c.name, t.name, c.max_length, c.precision, c.scale
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
                    string name = reader.GetString(0);
                    string type = reader.GetString(1);

                    if (variableLengthTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                    {
                        // Special "max" case. I'm actually not sure it's valid to have varchar(max) as a primary key because
                        // it exceeds the byte limit of an index field (900 bytes), but just in case
                        short length = reader.GetInt16(2);
                        type += length == -1 ? "(max)" : $"({length})";
                    }
                    else if (variablePrecisionTypes.Contains(type))
                    {
                        byte precision = reader.GetByte(3);
                        byte scale = reader.GetByte(4);
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
            string getUserTableColumnsQuery = $@"
                SELECT c.name, t.name, t.is_assembly_type
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
                    string columnName = reader.GetString(0);
                    string columnType = reader.GetString(1);
                    bool isAssemblyType = reader.GetBoolean(2);

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

                var conflictingColumnNames = userTableColumns.Intersect(SqlTriggerConstants.ReservedColumnNames).ToList();

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
                IF SCHEMA_ID(N'{SqlTriggerConstants.SchemaName}') IS NULL
                    EXEC ('CREATE SCHEMA {SqlTriggerConstants.SchemaName}');
            ";

            this._logger.LogDebugWithThreadId($"BEGIN CreateSchema Query={createSchemaQuery}");
            using (var createSchemaCommand = new SqlCommand(createSchemaQuery, connection, transaction))
            {
                var stopwatch = Stopwatch.StartNew();
                await createSchemaCommand.ExecuteNonQueryAsync(cancellationToken);
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
                IF OBJECT_ID(N'{SqlTriggerConstants.GlobalStateTableName}', 'U') IS NULL
                    CREATE TABLE {SqlTriggerConstants.GlobalStateTableName} (
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
                await createGlobalStateTableCommand.ExecuteNonQueryAsync(cancellationToken);
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
                IF NOT EXISTS (
                    SELECT * FROM {SqlTriggerConstants.GlobalStateTableName}
                    WHERE UserFunctionID = '{this._userFunctionId}' AND UserTableID = {userTableId}
                )
                    INSERT INTO {SqlTriggerConstants.GlobalStateTableName}
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
                IF OBJECT_ID(N'{leasesTableName}', 'U') IS NULL
                    CREATE TABLE {leasesTableName} (
                        {primaryKeysWithTypes},
                        {SqlTriggerConstants.LeasesTableChangeVersionColumnName} bigint NOT NULL,
                        {SqlTriggerConstants.LeasesTableAttemptCountColumnName} int NOT NULL,
                        {SqlTriggerConstants.LeasesTableLeaseExpirationTimeColumnName} datetime2,
                        PRIMARY KEY ({primaryKeys})
                    );
            ";

            this._logger.LogDebugWithThreadId($"BEGIN CreateLeasesTable Query={createLeasesTableQuery}");
            using (var createLeasesTableCommand = new SqlCommand(createLeasesTableQuery, connection, transaction))
            {
                var stopwatch = Stopwatch.StartNew();
                await createLeasesTableCommand.ExecuteNonQueryAsync(cancellationToken);
                long durationMs = stopwatch.ElapsedMilliseconds;
                this._logger.LogDebugWithThreadId($"END CreateLeasesTable Duration={durationMs}ms");
                return durationMs;
            }
        }

        async Task<ScaleMetrics> IScaleMonitor.GetMetricsAsync()
        {
            return await this.GetMetricsAsync();
        }

        public async Task<SqlTriggerMetrics> GetMetricsAsync()
        {
            Debug.Assert(!(this._changeMonitor is null));

            return new SqlTriggerMetrics
            {
                UnprocessedChangeCount = await this._changeMonitor.GetUnprocessedChangeCountAsync(),
                Timestamp = DateTime.UtcNow,
            };
        }

        ScaleStatus IScaleMonitor.GetScaleStatus(ScaleStatusContext context)
        {
            return this.GetScaleStatusWithTelemetry(context.WorkerCount, context.Metrics?.Cast<SqlTriggerMetrics>().ToArray());
        }

        public ScaleStatus GetScaleStatus(ScaleStatusContext<SqlTriggerMetrics> context)
        {
            return this.GetScaleStatusWithTelemetry(context.WorkerCount, context.Metrics?.ToArray());
        }

        private ScaleStatus GetScaleStatusWithTelemetry(int workerCount, SqlTriggerMetrics[] metrics)
        {
            var status = new ScaleStatus
            {
                Vote = ScaleVote.None,
            };

            var properties = new Dictionary<TelemetryPropertyName, string>(this._telemetryProps)
            {
                [TelemetryPropertyName.ScaleRecommendation] = $"{status.Vote}",
                [TelemetryPropertyName.TriggerMetrics] = metrics is null ? "null" : $"[{string.Join(", ", metrics.Select(metric => metric.UnprocessedChangeCount))}]",
                [TelemetryPropertyName.WorkerCount] = $"{workerCount}",
            };

            try
            {
                status = this.GetScaleStatusCore(workerCount, metrics);

                properties[TelemetryPropertyName.ScaleRecommendation] = $"{status.Vote}";
                TelemetryInstance.TrackEvent(TelemetryEventName.GetScaleStatus, properties);
            }
            catch (Exception ex)
            {
                this._logger.LogError($"Failed to get scale status for table '{this._userTable.FullName}' due to exception: {ex.GetType()}. Exception message: {ex.Message}");
                TelemetryInstance.TrackException(TelemetryErrorName.GetScaleStatus, ex, properties);
            }

            return status;
        }

        /// <summary>
        /// Returns scale recommendation i.e. whether to scale in or out the host application. The recommendation is
        /// made based on both the latest metrics and the trend of increase or decrease in the count of unprocessed
        /// changes in the user table. In all of the calculations, it is attempted to keep the number of workers minimum
        /// while also ensuring that the count of unprocessed changes per worker stays under the maximum limit.
        /// </summary>
        /// <param name="workerCount">The current worker count for the host application.</param>
        /// <param name="metrics">The collection of metrics samples to make the scale decision.</param>
        /// <returns></returns>
        private ScaleStatus GetScaleStatusCore(int workerCount, SqlTriggerMetrics[] metrics)
        {
            // We require minimum 5 samples to estimate the trend of variation in count of unprocessed changes with
            // certain reliability. These samples roughly cover the timespan of past 40 seconds.
            const int minSamplesForScaling = 5;

            // NOTE: please ensure the Readme file and other public documentation are also updated if this value ever
            // needs to be changed.
            const int maxChangesPerWorker = 1000;

            var status = new ScaleStatus
            {
                Vote = ScaleVote.None,
            };

            // Do not make a scale decision unless we have enough samples.
            if (metrics is null || (metrics.Length < minSamplesForScaling))
            {
                this._logger.LogInformation($"Requesting no-scaling: Insufficient metrics for making scale decision for table: '{this._userTable.FullName}'.");
                return status;
            }

            // Consider only the most recent batch of samples in the rest of the method.
            metrics = metrics.TakeLast(minSamplesForScaling).ToArray();

            string counts = string.Join(", ", metrics.Select(metric => metric.UnprocessedChangeCount));
            this._logger.LogInformation($"Unprocessed change counts: [{counts}], worker count: {workerCount}, maximum changes per worker: {maxChangesPerWorker}.");

            // Add worker if the count of unprocessed changes per worker exceeds the maximum limit.
            long lastUnprocessedChangeCount = metrics.Last().UnprocessedChangeCount;
            if (lastUnprocessedChangeCount > workerCount * maxChangesPerWorker)
            {
                status.Vote = ScaleVote.ScaleOut;
                this._logger.LogInformation($"Requesting scale-out: Found too many unprocessed changes for table: '{this._userTable.FullName}' relative to the number of workers.");
                return status;
            }

            // Check if there is a continuous increase or decrease in count of unprocessed changes.
            bool isIncreasing = true;
            bool isDecreasing = true;
            for (int index = 0; index < metrics.Length - 1; index++)
            {
                isIncreasing = isIncreasing && metrics[index].UnprocessedChangeCount < metrics[index + 1].UnprocessedChangeCount;
                isDecreasing = isDecreasing && (metrics[index + 1].UnprocessedChangeCount == 0 || metrics[index].UnprocessedChangeCount > metrics[index + 1].UnprocessedChangeCount);
            }

            if (isIncreasing)
            {
                // Scale out only if the expected count of unprocessed changes would exceed the combined limit after 30 seconds.
                DateTime referenceTime = metrics[metrics.Length - 1].Timestamp - TimeSpan.FromSeconds(30);
                SqlTriggerMetrics referenceMetric = metrics.First(metric => metric.Timestamp > referenceTime);
                long expectedUnprocessedChangeCount = (2 * metrics[metrics.Length - 1].UnprocessedChangeCount) - referenceMetric.UnprocessedChangeCount;

                if (expectedUnprocessedChangeCount > workerCount * maxChangesPerWorker)
                {
                    status.Vote = ScaleVote.ScaleOut;
                    this._logger.LogInformation($"Requesting scale-out: Found the unprocessed changes for table: '{this._userTable.FullName}' to be continuously increasing" +
                        " and may exceed the maximum limit set for the workers.");
                    return status;
                }
                else
                {
                    this._logger.LogDebug($"Avoiding scale-out: Found the unprocessed changes for table: '{this._userTable.FullName}' to be increasing" +
                        " but they may not exceed the maximum limit set for the workers.");
                }
            }

            if (isDecreasing)
            {
                // Scale in only if the count of unprocessed changes will not exceed the combined limit post the scale-in operation.
                if (lastUnprocessedChangeCount <= (workerCount - 1) * maxChangesPerWorker)
                {
                    status.Vote = ScaleVote.ScaleIn;
                    this._logger.LogInformation($"Requesting scale-in: Found table: '{this._userTable.FullName}' to be either idle or the unprocessed changes to be continuously decreasing.");
                    return status;
                }
                else
                {
                    this._logger.LogDebug($"Avoiding scale-in: Found the unprocessed changes for table: '{this._userTable.FullName}' to be decreasing" +
                        " but they are high enough to require all existing workers for processing.");
                }
            }

            this._logger.LogInformation($"Requesting no-scaling: Found the number of unprocessed changes for table: '{this._userTable.FullName}' to not require scaling.");
            return status;
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