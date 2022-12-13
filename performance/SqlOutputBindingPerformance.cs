// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples;
using BenchmarkDotNet.Attributes;


namespace Microsoft.Azure.WebJobs.Extensions.Sql.Performance
{
    [MemoryDiagnoser]
    public class SqlOutputBindingPerformance : IntegrationTestBase
    {
        [GlobalSetup]
        public void StartAddProductsArrayFunction()
        {
            this.StartFunctionHost(nameof(AddProductsArray), SupportedLanguages.CSharp);
        }

        [Benchmark]
        [Arguments(1)]
        [Arguments(10)]
        [Arguments(100)]
        [Arguments(1000)]
        public async Task<HttpResponseMessage> AddProductsArrayTest(int count)
        {
            Product[] productsToAdd = GetProductsWithSameCost(count, 100);
            return await this.SendOutputPostRequest("addproducts-array", Utils.SerializeObject(productsToAdd));
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            // Delete all rows in Products table after each iteration
            this.ExecuteNonQuery("TRUNCATE TABLE Products");
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            // Delete the database
            this.Dispose();
        }
    }
}