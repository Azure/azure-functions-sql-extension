// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.TriggerBindingSamples;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Performance
{
    [MemoryDiagnoser]
    public class SqlTriggerBindingPerformance_PollingIntervalOverride : SqlTriggerBindingPerformanceTestBase
    {
        [Benchmark]
        [Arguments(1000, 500)]
        [Arguments(1000, 100)]
        [Arguments(1000, 10)]
        [Arguments(1000, 1)]
        public async Task Run(int count, int pollingIntervalMs)
        {
            this.StartFunctionHost(
                nameof(ProductsTrigger),
                SupportedLanguages.CSharp,
                environmentVariables: new Dictionary<string, string>() {
                    { "Sql_Trigger_PollingIntervalMs", pollingIntervalMs.ToString() }
                });
            await this.WaitForProductChanges(
                1,
                count,
                SqlChangeOperation.Insert,
                () => { this.InsertProducts(1, count); return Task.CompletedTask; },
                id => $"Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(1, count, pollingIntervalMs: pollingIntervalMs));
        }
    }
}