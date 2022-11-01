// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Performance
{
    [MemoryDiagnoser]
    public class SqlInputBindingPerformance : IntegrationTestBase
    {
        [GlobalSetup]
        public void GlobalSetup()
        {
            this.StartFunctionHost(nameof(GetProductsTopN), SupportedLanguages.CSharp);
            Product[] products = GetProductsWithSameCost(10000, 100);
            this.InsertProducts(products);
        }

        [Benchmark]
        [Arguments("1")]
        [Arguments("10")]
        [Arguments("100")]
        [Arguments("1000")]
        [Arguments("10000")]
        public async Task<HttpResponseMessage> GetProductsTest(string count)
        {
            return await this.SendInputRequest("getproductstopn", count);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.Dispose();
        }
    }
}