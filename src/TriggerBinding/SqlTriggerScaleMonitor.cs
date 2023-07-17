// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry;
using static Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry.Telemetry;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using MoreLinq;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Makes the scale decision for incremental scaling(+1, 0, -1) for workers required based on unprocessed changes.
    /// Guidance for scaling information can be found here https://learn.microsoft.com/en-us/azure/azure-functions/event-driven-scaling
    /// </summary>
    internal sealed class SqlTriggerScaleMonitor : IScaleMonitor<SqlTriggerMetrics>
    {
        private readonly ILogger _logger;
        private readonly SqlObject _userTable;
        private readonly SqlTriggerMetricsProvider _metricsProvider;
        private readonly IDictionary<TelemetryPropertyName, string> _telemetryProps = new Dictionary<TelemetryPropertyName, string>();
        private readonly int _maxChangesPerWorker;

        public SqlTriggerScaleMonitor(string userFunctionId, SqlObject userTable, string connectionString, int maxChangesPerWorker, ILogger logger)
        {
            _ = !string.IsNullOrEmpty(userFunctionId) ? true : throw new ArgumentNullException(userFunctionId);
            _ = userTable != null ? true : throw new ArgumentNullException(nameof(userTable));
            this._userTable = userTable;
            // Do not convert the scale-monitor ID to lower-case string since SQL table names can be case-sensitive
            // depending on the collation of the current database.
            this.Descriptor = new ScaleMonitorDescriptor($"{userFunctionId}-SqlTrigger-{this._userTable.FullName}", userFunctionId);
            this._metricsProvider = new SqlTriggerMetricsProvider(connectionString, logger, this._userTable, userFunctionId);
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._maxChangesPerWorker = maxChangesPerWorker;
        }

        public ScaleMonitorDescriptor Descriptor { get; }
        async Task<ScaleMetrics> IScaleMonitor.GetMetricsAsync()
        {
            return await this.GetMetricsAsync();
        }

        public async Task<SqlTriggerMetrics> GetMetricsAsync()
        {
            return await this._metricsProvider.GetMetricsAsync().ConfigureAwait(false);
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

            var status = new ScaleStatus
            {
                Vote = ScaleVote.None,
            };

            // Do not make a scale decision unless we have enough samples.
            if (metrics is null || (metrics.Length < minSamplesForScaling))
            {
                return status;
            }

            // Consider only the most recent batch of samples in the rest of the method.
            metrics = metrics.TakeLast(minSamplesForScaling).ToArray();

            string counts = string.Join(", ", metrics.Select(metric => metric.UnprocessedChangeCount));

            // Add worker if the count of unprocessed changes per worker exceeds the maximum limit.
            long lastUnprocessedChangeCount = metrics.Last().UnprocessedChangeCount;
            if (lastUnprocessedChangeCount > workerCount * this._maxChangesPerWorker)
            {
                status.Vote = ScaleVote.ScaleOut;
                this._logger.LogDebug($"Requesting scale-out: Found too many unprocessed changes: {lastUnprocessedChangeCount} for table: '{this._userTable.FullName}' relative to the number of workers.");
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

                if (expectedUnprocessedChangeCount > workerCount * this._maxChangesPerWorker)
                {
                    status.Vote = ScaleVote.ScaleOut;
                    this._logger.LogDebug($"Requesting scale-out: Found the unprocessed changes for table: '{this._userTable.FullName}' to be continuously increasing" +
                        " and may exceed the maximum limit set for the workers.");
                    return status;
                }
            }

            if (isDecreasing)
            {
                // Scale in only if the count of unprocessed changes will not exceed the combined limit post the scale-in operation.
                if (lastUnprocessedChangeCount <= (workerCount - 1) * this._maxChangesPerWorker)
                {
                    status.Vote = ScaleVote.ScaleIn;
                    this._logger.LogDebug($"Requesting scale-in: Found table: '{this._userTable.FullName}' to be either idle or the unprocessed changes to be continuously decreasing.");
                    return status;
                }
            }
            return status;
        }
    }
}