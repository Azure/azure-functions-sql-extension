// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlTriggerUtils;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Represents the target based scaler returning a target worker count.
    /// </summary>
    internal sealed class SqlTriggerTargetScaler : ITargetScaler
    {
        private readonly SqlTriggerMetricsProvider _metricsProvider;
        private readonly int _maxChangesPerWorker;
        private readonly ILogger _logger;
        private readonly string _connectionString;
        private readonly SqlObject _userTable;
        private static readonly DateTime _firstTableCreationWarmupAttempt = DateTime.MinValue;


        public SqlTriggerTargetScaler(string userFunctionId, SqlObject userTable, string userDefinedLeasesTableName, string connectionString, int maxChangesPerWorker, int appLockTimeoutMs, ILogger logger)
        {
            this._metricsProvider = new SqlTriggerMetricsProvider(connectionString, logger, userTable, userFunctionId, userDefinedLeasesTableName, appLockTimeoutMs);
            this.TargetScalerDescriptor = new TargetScalerDescriptor(userFunctionId);
            this._maxChangesPerWorker = maxChangesPerWorker;
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._connectionString = !string.IsNullOrEmpty(connectionString) ? connectionString : throw new ArgumentNullException(nameof(connectionString));
            this._userTable = userTable ?? throw new ArgumentNullException(nameof(userTable));
        }

        public TargetScalerDescriptor TargetScalerDescriptor { get; }

        public async Task<TargetScalerResult> GetScaleResultAsync(TargetScalerContext context)
        {
            try
            {
                SqlTriggerMetrics metrics = await this._metricsProvider.GetMetricsAsync();

                // Instance concurrency value is set by the functions host when dynamic concurrency is enabled. See https://learn.microsoft.com/en-us/azure/azure-functions/functions-concurrency for more details.
                int concurrency = context.InstanceConcurrency ?? this._maxChangesPerWorker;

                return GetScaleResultInternal(concurrency, metrics.UnprocessedChangeCount);
            }
            catch (Exception ex)
            {
                // If the exception is SQL exception and indicates that the object name is invalid, it means that the global state and leases table are not created
                // Check for the error number 208 https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/mssqlserver-208-database-engine-error?view=sql-server-ver17
                if (ex is SqlException sqlEx && sqlEx.Number == 208)
                {
                    // If it's been 2 minutes since we first spun up the worker and the table still isn't created then stop trying
                    // since it likely means something else is wrong we can't fix automatically, and we don't want to leave an
                    // instance running forever.
                    this._logger.LogWarning("Invalid object name detected. SQL trigger tables not found.");
                    if (_firstTableCreationWarmupAttempt != DateTime.MinValue && DateTime.UtcNow - _firstTableCreationWarmupAttempt > TimeSpan.FromMinutes(2))
                    {
                        this._logger.LogWarning("Returning 0 as the target worker count since the GetMetrics query threw an 'Invalid object name detected' error and we've exceeded the warmup period for scaling up a new instance to create the required state tables.");
                        return new TargetScalerResult
                        {
                            TargetWorkerCount = 0
                        };
                    }
                    else
                    {
                        // Check if there are any changes in the user table. Since we don't have a leases table that means
                        // we haven't processed any of the changes yet so we can just check if there's any changes
                        // for the table at all (no last sync point)
                        int changes = await GetChangeCountFromChangeTrackingAsync(this._connectionString, this._userTable, this._logger, CancellationToken.None);
                        // If there are changes in the user table, we spin up worker(s) to start handling those changes.  
                        // This will also create the global state and leases table, which will allow the scaling logic to start working as intended.
                        if (changes > 0)
                        {
                            this._logger.LogWarning("There are changes in the change-tracking table for the user table, but the global state and leases table are not created. Spinning up worker instances to create those tables and start processing changes.");
                            return new TargetScalerResult
                            {
                                TargetWorkerCount = (int)Math.Ceiling(changes / (decimal)(context.InstanceConcurrency ?? this._maxChangesPerWorker))
                            };
                        }
                    }

                }
                // If the exception is not related to the invalid object name Or if there are no changes in the change tracking table for the user table yet.
                this._logger.LogError("An error occurred while getting the scale result for SQL trigger. Exception: {ExceptionMessage}", ex.Message);
                throw;
            }
        }

        internal static TargetScalerResult GetScaleResultInternal(int concurrency, long unprocessedChangeCount)
        {
            if (concurrency < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(concurrency), $"Unexpected concurrency='{concurrency}' - the value must be > 0.");
            }

            int targetWorkerCount = (int)Math.Ceiling(unprocessedChangeCount / (decimal)concurrency);

            return new TargetScalerResult
            {
                TargetWorkerCount = targetWorkerCount
            };
        }
    }
}