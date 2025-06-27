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
        private static DateTime _lastTableCheck = DateTime.MinValue;


        public SqlTriggerTargetScaler(string userFunctionId, SqlObject userTable, string userDefinedLeasesTableName, string connectionString, int maxChangesPerWorker, ILogger logger)
        {
            this._metricsProvider = new SqlTriggerMetricsProvider(connectionString, logger, userTable, userFunctionId, userDefinedLeasesTableName);
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
                // Check for the error number 208 or the error message https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/mssqlserver-208-database-engine-error?view=sql-server-ver17
                if (ex is SqlException sqlEx && (sqlEx.Number == 208 || sqlEx.Message.Contains("Invalid object name") ||
                                   (sqlEx.InnerException != null &&
                                    sqlEx.InnerException.Message.Contains("Invalid object name"))))
                {
                    // Check if we already tried to spin up the worker that starts the listener which will create those tables.
                    // If we have already tried, we will return 0 as the target worker count.
                    this._logger.LogWarning("Invalid object name detected. SQL trigger tables not found.");
                    if (_lastTableCheck != DateTime.MinValue && DateTime.UtcNow - _lastTableCheck < TimeSpan.FromMinutes(2))
                    {
                        // If we have already checked within the last 5 minutes, we will return 0 as the target worker count.
                        this._logger.LogWarning("Returning 0 as the target worker count since we have already checked for the SQL trigger tables within the last 5 minutes.");
                        return new TargetScalerResult
                        {
                            TargetWorkerCount = 0
                        };
                    }
                    else
                    {
                        // Check if there are any changes in the user table.
                        int changes = await GetNumberOfChangesAsync(this._connectionString, this._userTable, this._logger, CancellationToken.None);
                        // If there are changes in the user table, we need to spin up a worker to create the global state and leases table.
                        if (changes > 0)
                        {
                            this._logger.LogWarning("There are changes in the change-tracking table for the user table, but the global state and leases table are not created. Spinning up a worker to create those tables.");
                            _lastTableCheck = DateTime.UtcNow;
                            return new TargetScalerResult
                            {
                                TargetWorkerCount = 1
                            };
                        }
                    }

                }
                // If the exception is not related to the invalid object name Or if there are no changes in the change tracking table for the user table yet.
                // We can safely return 0 as the target worker count, indicating that no workers are needed at this time.
                this._logger.LogWarning("An error occurred while getting the scale result for SQL trigger. Returning 0 as the target worker count. Exception: {ExceptionMessage}", ex.Message);
                return new TargetScalerResult
                {
                    TargetWorkerCount = 0
                };
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