// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlTriggerMetricsProvider<T>
    {
        private readonly SqlTableChangeMonitor<T> _changeMonitor;

        public SqlTriggerMetricsProvider(SqlTableChangeMonitor<T> changeMonitor)
        {
            this._changeMonitor = changeMonitor ?? throw new ArgumentNullException(nameof(changeMonitor));
        }
        public async Task<SqlTriggerMetrics> GetMetricsAsync()
        {
            return new SqlTriggerMetrics
            {
                UnprocessedChangeCount = await this._changeMonitor.GetUnprocessedChangeCountAsync(),
                Timestamp = DateTime.UtcNow,
            };
        }
    }
}