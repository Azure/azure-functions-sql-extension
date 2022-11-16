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
    public class SqlTriggerBindingPerformance_BatchOverride : SqlTriggerBindingPerformanceTestBase
    {

        [Params(100, 1000)]
        public int BatchSize;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.SetChangeTrackingForTable("Products", true);
            this.StartFunctionHost(
                nameof(ProductsTrigger),
                SupportedLanguages.CSharp,
                environmentVariables: new Dictionary<string, string>() {
                    { "Sql_Trigger_BatchSize", this.BatchSize.ToString() }
                });
        }

        [Benchmark]
        [Arguments(0.1)]
        [Arguments(0.5)]
        [Arguments(1)]
        [Arguments(5)]
        public async Task Run(double numBatches)
        {
            await this.RunWithDisposeOnError(async () =>
            {
                int count = (int)(numBatches * this.BatchSize);
                await this.WaitForProductChanges(
                    1,
                    count,
                    SqlChangeOperation.Insert,
                    () => { this.InsertProducts(1, count); return Task.CompletedTask; },
                    id => $"Product {id}",
                    id => id * 100,
                    this.GetBatchProcessingTimeout(1, count, batchSize: this.BatchSize));
            });
        }
    }
}