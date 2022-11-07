// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.TriggerBindingSamples;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Performance
{
    [MemoryDiagnoser]
    public class SqlTriggerBindingPerformance : SqlTriggerBindingPerformanceTestBase
    {
        [GlobalSetup]
        public void GlobalSetup()
        {
            this.SetChangeTrackingForTable("Products", true);
            this.StartFunctionHost(nameof(ProductsTrigger), SupportedLanguages.CSharp);
        }

        [Benchmark]
        [Arguments(1)]
        [Arguments(10)]
        [Arguments(100)]
        [Arguments(1000)]
        public async Task ProductsTriggerTest(int count)
        {
            await this.WaitForProductChanges(
                1,
                count,
                SqlChangeOperation.Insert,
                () => { this.InsertProducts(1, count); return Task.CompletedTask; },
                id => $"Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(1, count));
        }
    }
}