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
        private readonly string _userFunctionId;
        private readonly string _oldUserFunctionId;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly SqlOptions _sqlOptions;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        private readonly IDictionary<TelemetryPropertyName, string> _telemetryProps = new Dictionary<TelemetryPropertyName, string>();
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
        /// <param name="userFunctionId">Unique identifier for the user function</param>
        /// <param name="oldUserFunctionId">deprecated user function id value created using hostId for the user function</param>
        /// <param name="executor">Defines contract for triggering user function</param>
        /// <param name="sqlOptions"></param>
        /// <param name="logger">Facilitates logging of messages</param>
        /// <param name="configuration">Provides configuration values</param>
        public SqlTriggerListener(string connectionString, string tableName, string userDefinedLeasesTableName, string userFunctionId, string oldUserFunctionId, ITriggeredFunctionExecutor executor, SqlOptions sqlOptions, ILogger logger, IConfiguration configuration)
        {
            this._connectionString = !string.IsNullOrEmpty(connectionString) ? connectionString : throw new ArgumentNullException(nameof(connectionString));
            this._userTable = !string.IsNullOrEmpty(tableName) ? new SqlObject(tableName) : throw new ArgumentNullException(nameof(tableName));
            this._userDefinedLeasesTableName = userDefinedLeasesTableName;
            this._userFunctionId = !string.IsNullOrEmpty(userFunctionId) ? userFunctionId : throw new ArgumentNullException(nameof(userFunctionId));
            this._oldUserFunctionId = oldUserFunctionId;
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
                    IReadOnlyList<(string name, string type)> primaryKeyColumns = GetPrimaryKeyColumnsAsync(connection, userTableId, this._logger, this._userTable.FullName, cancellationToken);
                    IReadOnlyList<string> userTableColumns = this.GetUserTableColumns(connection, userTableId, cancellationToken);

                    string bracketedLeasesTableName = GetBracketedLeasesTableName(this._userDefinedLeasesTableName, this._userFunctionId, userTableId);
                    this._telemetryProps[TelemetryPropertyName.LeasesTableName] = bracketedLeasesTableName;

                    var transactionSw = Stopwatch.StartNew();
                    long createdSchemaDurationMs = 0L, createGlobalStateTableDurationMs = 0L, insertGlobalStateTableRowDurationMs = 0L, createLeasesTableDurationMs = 0L;

                    using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
                    {
                        createdSchemaDurationMs = await CreateSchemaAsync(connection, transaction, this._telemetryProps, this._logger, cancellationToken);
                        createGlobalStateTableDurationMs = await CreateGlobalStateTableAsync(connection, transaction, this._telemetryProps, this._logger, cancellationToken);
                        insertGlobalStateTableRowDurationMs = await InsertGlobalStateTableRowAsync(connection, transaction, userTableId, this._userTable, this._oldUserFunctionId, this._userFunctionId, this._logger, cancellationToken);
                        createLeasesTableDurationMs = await CreateLeasesTableAsync(connection, transaction, bracketedLeasesTableName, primaryKeyColumns, this._oldUserFunctionId, this._userFunctionId, this._telemetryProps, this._logger, cancellationToken);
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
        private IReadOnlyList<string> GetUserTableColumns(SqlConnection connection, int userTableId, CancellationToken cancellationToken)
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