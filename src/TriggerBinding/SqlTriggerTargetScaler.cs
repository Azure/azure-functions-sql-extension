// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal sealed class SqlTriggerTargetScaler<T> : ITargetScaler
    {
        private readonly string _userFunctionId;
        private readonly ILogger _logger;
        private readonly SqlTriggerMetricsProvider<T> _metricsProvider;
        private readonly int _maxChangesPerWorker;

        internal SqlTriggerTargetScaler(string userFunctionId, ILogger logger, int maxChangesPerWorker, SqlTableChangeMonitor<T> changeMonitor)
        {
            this._userFunctionId = !string.IsNullOrEmpty(userFunctionId) ? userFunctionId : throw new ArgumentNullException(userFunctionId);
            _ = changeMonitor ?? throw new ArgumentNullException(nameof(changeMonitor));
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._metricsProvider = new SqlTriggerMetricsProvider<T>(changeMonitor);
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
            int concurrency = context.InstanceConcurrency ?? this._maxChangesPerWorker;

            if (concurrency < 1)
            {
                throw new ArgumentOutOfRangeException($"Unexpected concurrency='{concurrency}' - the value must be > 0.");
            }

            int targetWorkerCount = (int)Math.Ceiling(unprocessedChangeCount / (decimal)concurrency);

            this._logger.LogInformation($"Target worker count for function '{this._userFunctionId}' is '{targetWorkerCount}' UnprocessedChangeCount ='{this._maxChangesPerWorker}', Concurrency='{concurrency}').");

            return new TargetScalerResult
            {
                TargetWorkerCount = targetWorkerCount
            };
        }
    }
}