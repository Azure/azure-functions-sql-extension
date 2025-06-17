// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlTriggerUtils;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlBindingUtilities;
using static Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry.Telemetry;
using System.Diagnostics;


namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlScalerProvider : IScaleMonitorProvider, ITargetScalerProvider
    {
        private readonly SqlTriggerScaleMonitor _scaleMonitor;
        private readonly SqlTriggerTargetScaler _targetScaler;
        private readonly string _connectionString;
        private readonly IDictionary<TelemetryPropertyName, string> _telemetryProps = new Dictionary<TelemetryPropertyName, string>();
        private readonly SqlObject _userTable;
        private readonly string _userFunctionId;
        private readonly int _maxChangesPerWorker;
        private readonly ILogger _logger;
        private readonly bool _hasConfiguredMaxChangesPerWorker;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlScalerProvider"/> class.
        /// Use for obtaining scale metrics by scale controller.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="triggerMetadata"></param>
        public SqlScalerProvider(IServiceProvider serviceProvider, TriggerMetadata triggerMetadata)
        {
            IConfiguration config = serviceProvider.GetService<IConfiguration>();
            ILoggerFactory loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            ILogger logger = loggerFactory.CreateLogger(LogCategories.CreateTriggerCategory("Sql"));
            SqlMetaData sqlMetadata = JsonConvert.DeserializeObject<SqlMetaData>(triggerMetadata.Metadata.ToString());
            sqlMetadata.ResolveProperties(serviceProvider.GetService<INameResolver>());
            this._userTable = new SqlObject(sqlMetadata.TableName);
            this._connectionString = GetConnectionString(sqlMetadata.ConnectionStringSetting, config);
            IOptions<SqlOptions> options = serviceProvider.GetService<IOptions<SqlOptions>>();
            int configOptionsMaxChangesPerWorker = options.Value.MaxChangesPerWorker;
            this._hasConfiguredMaxChangesPerWorker = configOptionsMaxChangesPerWorker != 0;
            int configAppSettingsMaxChangesPerWorker = config.GetValue<int>(SqlTriggerConstants.ConfigKey_SqlTrigger_MaxChangesPerWorker);
            // Override the maxChangesPerWorker value from config if the value is set in the trigger appsettings
            int maxChangesPerWorker = configAppSettingsMaxChangesPerWorker != 0 ? configAppSettingsMaxChangesPerWorker : configOptionsMaxChangesPerWorker != 0 ? configOptionsMaxChangesPerWorker : SqlOptions.DefaultMaxChangesPerWorker;
            this._maxChangesPerWorker = maxChangesPerWorker;
            this._logger = logger;
            string userDefinedLeasesTableName = sqlMetadata.LeasesTableName;
            this._userFunctionId = sqlMetadata.UserFunctionId;

            this._scaleMonitor = new SqlTriggerScaleMonitor(this._userFunctionId, this._userTable, userDefinedLeasesTableName, this._connectionString, maxChangesPerWorker, logger);
            this._targetScaler = new SqlTriggerTargetScaler(this._userFunctionId, this._userTable, userDefinedLeasesTableName, this._connectionString, maxChangesPerWorker, logger);
        }

        public IScaleMonitor GetMonitor()
        {
            return this._scaleMonitor;
        }

        public ITargetScaler GetTargetScaler()
        {
            return this._targetScaler;
        }

        public static async Task<SqlScalerProvider> CreateAsync(IServiceProvider serviceProvider, TriggerMetadata triggerMetadata)
        {
            var provider = new SqlScalerProvider(serviceProvider, triggerMetadata);
            await provider.InitializeAsync();
            return provider;
        }


        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            using (var connection = new SqlConnection(this._connectionString))
            {
                ServerProperties serverProperties = await GetServerTelemetryProperties(connection, this._logger, cancellationToken);
                this._telemetryProps.AddConnectionProps(connection, serverProperties);
                await VerifyDatabaseSupported(connection, this._logger, cancellationToken);

                int userTableId = await GetUserTableIdAsync(connection, this._userTable, this._logger, cancellationToken);
                IReadOnlyList<(string name, string type)> primaryKeyColumns = GetPrimaryKeyColumnsAsync(connection, userTableId, this._logger, this._userTable.FullName, cancellationToken);

                string bracketedLeasesTableName = GetBracketedLeasesTableName(null, this._userFunctionId, userTableId);
                this._telemetryProps[TelemetryPropertyName.LeasesTableName] = bracketedLeasesTableName;

                var transactionSw = Stopwatch.StartNew();
                long createdSchemaDurationMs = 0L, createGlobalStateTableDurationMs = 0L, insertGlobalStateTableRowDurationMs = 0L, createLeasesTableDurationMs = 0L;
                using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
                {
                    createdSchemaDurationMs = await CreateSchemaAsync(connection, transaction, this._telemetryProps, this._logger, cancellationToken);
                    createGlobalStateTableDurationMs = await CreateGlobalStateTableAsync(connection, transaction, this._telemetryProps, this._logger, cancellationToken);
                    insertGlobalStateTableRowDurationMs = await InsertGlobalStateTableRowAsync(connection, transaction, userTableId, this._userTable, null, this._userFunctionId, this._logger, cancellationToken);
                    createLeasesTableDurationMs = await CreateLeasesTableAsync(connection, transaction, bracketedLeasesTableName, primaryKeyColumns, null, this._userFunctionId, this._telemetryProps, this._logger, cancellationToken);
                    transaction.Commit();
                }

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
                    TelemetryEventName.InitializeScaleProvider,
                    new Dictionary<TelemetryPropertyName, string>(this._telemetryProps) {
                        { TelemetryPropertyName.HasConfiguredMaxChangesPerWorker, this._hasConfiguredMaxChangesPerWorker.ToString() }
                    },
                    measures);
            }
        }

        internal class SqlMetaData
        {
            [JsonProperty]
            public string ConnectionStringSetting { get; set; }

            [JsonProperty]
            public string TableName { get; set; }

            [JsonProperty]
            public string LeasesTableName { get; set; }

            [JsonProperty]
            public string UserFunctionId { get; set; }

            public void ResolveProperties(INameResolver resolver)
            {
                if (resolver != null)
                {
                    this.ConnectionStringSetting = resolver.ResolveWholeString(this.ConnectionStringSetting);
                }
            }
        }
    }
}