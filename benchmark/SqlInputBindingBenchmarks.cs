// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Benchmark
{
    public class InputBindingBenchmarks : IntegrationTestBase
    {
        public InputBindingBenchmarks() : base()
        {
            this.StartFunctionHost(nameof(GetProducts), SupportedLanguages.CSharp);

            // Generate T-SQL to insert n rows of data with cost
            Product[] products = GetProductsWithSameCost(10, 100);
            this.InsertProducts(products);
        }

        [Benchmark]
        [Arguments("getproducts", "100")]
        public async Task<HttpResponseMessage> GetProductsTest(string function, string args)
        {
            // Run the function
            return await this.SendInputRequest(function, args);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.Dispose();
        }
    }
}