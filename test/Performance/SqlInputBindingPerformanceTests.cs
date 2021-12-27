// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Performance
{
    [Collection("PerformanceTests")]
    public class SqlInputBindingPerformanceTests : IntegrationTestBase
    {
        public SqlInputBindingPerformanceTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<HttpResponseMessage> SendInputRequest(string functionName, string query = "")
        {
            string requestUri = $"http://localhost:{this.Port}/api/{functionName}/{query}";

            return await this.SendGetRequest(requestUri);
        }

        [Fact]
        public async void GetProductsPerformanceTest()
        {
            this.StartFunctionHost(nameof(GetProducts));

            // Generate T-SQL to insert n rows of data with cost
            Product[] products = GetProductsWithSameCost(100, 100);
            this.InsertProducts(products);

            int numOperations = 100;
            long totalTime = 0;
            long maxTime = 0;
            long minTime = int.MaxValue;
            int indexFastest = -1;
            int indexSlowest = -1;

            var time100Operations = Stopwatch.StartNew();

            for (int i = 0; i <= numOperations; i++)
            {
                long time = 0;
                var timePerOperation = Stopwatch.StartNew();

                await this.SendInputRequest("getproducts", "100");

                timePerOperation.Stop();
                time = timePerOperation.ElapsedMilliseconds;

                // Update operation statistics
                if (maxTime < time)
                {
                    indexSlowest = i;
                    maxTime = time;
                }
                if (minTime > time)
                {
                    indexFastest = i;
                    minTime = time;
                }
            }

            time100Operations.Stop();
            totalTime = time100Operations.ElapsedMilliseconds;

            this.TestOutput.WriteLine("  Slowest time:  #{0}/{1} = {2} ms",
                indexSlowest, numOperations, maxTime);
            this.TestOutput.WriteLine("  Fastest time:  #{0}/{1} = {2} ms",
                indexFastest, numOperations, minTime);
            this.TestOutput.WriteLine("  Average time:  {0} ms", totalTime / numOperations);
            this.TestOutput.WriteLine("  Total time looping through {0} operations: {1} ms", numOperations, totalTime);
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

        private static Product[] GetProductsWithSameCostAndName(int n, int cost, string name, int offset = 0)
        {
            var result = new Product[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = new Product
                {
                    ProductID = i + offset,
                    Name = name,
                    Cost = cost
                };
            }
            return result;
        }

        private void InsertProducts(Product[] products)
        {
            if (products.Length == 0)
            {
                return;
            }

            var queryBuilder = new StringBuilder();
            foreach (Product p in products)
            {
                queryBuilder.AppendLine($"INSERT INTO dbo.Products VALUES({p.ProductID}, '{p.Name}', {p.Cost});");
            }

            this.ExecuteNonQuery(queryBuilder.ToString());
        }
    }
}
