// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Represents the target based scaler returning a target worker count.
    /// </summary>
    public class SqlTriggerTargetScaler : ITargetScaler
    {
        private readonly string _userFunctionId;
        private readonly ILogger _logger;
        private readonly SqlTriggerMetricsProvider _metricsProvider;
        private readonly int _maxChangesPerWorker;

        public SqlTriggerTargetScaler(string userFunctionId, string userTableName, string connectionString, int maxChangesPerWorker, ILogger logger)
        {
            _ = !string.IsNullOrEmpty(userTableName) ? true : throw new ArgumentNullException(userTableName);
            this._userFunctionId = !string.IsNullOrEmpty(userFunctionId) ? userFunctionId : throw new ArgumentNullException(userFunctionId);
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._metricsProvider = new SqlTriggerMetricsProvider(connectionString, logger, new SqlObject(userTableName), userFunctionId);
            this.TargetScalerDescriptor = new TargetScalerDescriptor(userFunctionId);
            this._maxChangesPerWorker = maxChangesPerWorker;
        }

        public TargetScalerDescriptor TargetScalerDescriptor { get; }

        public async Task<TargetScalerResult> GetScaleResultAsync(TargetScalerContext context)
        {
            SqlTriggerMetrics metrics = await this._metricsProvider.GetMetricsAsync();

            return this.GetScaleResultInternal(context, metrics.UnprocessedChangeCount);
        }

        internal TargetScalerResult GetScaleResultInternal(TargetScalerContext context, long unprocessedChangeCount)
        {
            // Instance concurrency value is set by the functions host when dynamic concurrency is enabled. See https://learn.microsoft.com/en-us/azure/azure-functions/functions-concurrency for more details.
            int concurrency = context.InstanceConcurrency ?? this._maxChangesPerWorker;

            if (concurrency < 1)
            {
                throw new ArgumentOutOfRangeException($"Unexpected concurrency='{concurrency}' - the value must be > 0.");
            }

            int targetWorkerCount = (int)Math.Ceiling(unprocessedChangeCount / (decimal)concurrency);

            this._logger.LogInformation($"Target worker count for function '{this._userFunctionId}' is '{targetWorkerCount}' UnprocessedChangeCount ='{unprocessedChangeCount}', Concurrency='{concurrency}').");

            return new TargetScalerResult
            {
                TargetWorkerCount = targetWorkerCount
            };
        }
    }
}