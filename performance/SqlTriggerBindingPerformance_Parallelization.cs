// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.TriggerBindingSamples;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Performance
{
    [MemoryDiagnoser]
    public class SqlTriggerBindingPerformance_Parallelization : SqlTriggerBindingPerformanceTestBase
    {
        [Params(2, 5)]
        public int HostCount;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.SetChangeTrackingForTable("Products", true);
            for (int i = 0; i < this.HostCount; ++i)
            {
                this.StartFunctionHost(nameof(ProductsTrigger), SupportedLanguages.CSharp);
            }
        }

        [Benchmark]
        public async Task MultiHost()
        {
            int firstId = 1;
            int lastId = 90;
            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Insert,
                () => { this.InsertProducts(firstId, lastId); return Task.CompletedTask; },
                id => $"Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId));

            firstId = 1;
            lastId = 60;
            // All table columns (not just the columns that were updated) would be returned for update operation.
            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Update,
                () => { this.UpdateProducts(firstId, lastId); return Task.CompletedTask; },
                id => $"Updated Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId));

            firstId = 31;
            lastId = 90;
            // The properties corresponding to non-primary key columns would be set to the C# type's default values
            // (null and 0) for delete operation.
            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Delete,
                () => { this.DeleteProducts(firstId, lastId); return Task.CompletedTask; },
                _ => null,
                _ => 0,
                this.GetBatchProcessingTimeout(firstId, lastId));
        }
    }
}