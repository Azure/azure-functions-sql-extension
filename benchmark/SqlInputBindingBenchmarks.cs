// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples;
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

        private async Task<HttpResponseMessage> SendInputRequest(string functionName, string query = "")
        {
            string requestUri = $"http://localhost:{this.Port}/api/{functionName}/{query}";

            return await this.SendGetRequest(requestUri);
        }

        private static Product[] GetProductsWithSameCost(int n, int cost)
        {
            var result = new Product[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = new Product
                {
                    ProductID = i,
                    Name = "test",
                    Cost = cost
                };
            }
            return result;
        }
    }
}