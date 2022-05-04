// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class SqlInputBindingJSIntegrationTests : IntegrationTestBase
    {
        private readonly string workingDirectoryFolder = string.Format("..{0}..{0}..{0}..{0}samples{0}samples-js", Path.DirectorySeparatorChar);
        public SqlInputBindingJSIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<HttpResponseMessage> SendInputRequest(string functionName, string query = "")
        {
            string requestUri = $"http://localhost:{this.Port}/api/{functionName}/{query}";

            return await this.SendGetRequest(requestUri);
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(1, -500)]
        [InlineData(100, 500)]
        public async void GetProductsTest(int n, int cost)
        {
            this.StartFunctionHost("GetProductsByCost", false, this.workingDirectoryFolder);

            // Generate T-SQL to insert n rows of data with cost
            Product[] products = GetProductsWithSameCost(n, cost);
            this.InsertProducts(products);

            // Run the function
            HttpResponseMessage response = await this.SendInputRequest("getproducts", cost.ToString());

            // Verify result
            string expectedResponse = JsonConvert.SerializeObject(products);
            string actualResponse = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedResponse, actualResponse.Trim().Replace(" ", "").Replace(Environment.NewLine, ""), StringComparer.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(0, 99)]
        [InlineData(1, -999)]
        [InlineData(100, 999)]
        public async void GetProductsStoredProcedureTest(int n, int cost)
        {
            this.StartFunctionHost("GetProductsStoredProcedure", false, this.workingDirectoryFolder);

            // Generate T-SQL to insert n rows of data with cost
            Product[] products = GetProductsWithSameCost(n, cost);
            this.InsertProducts(products);

            // Run the function
            HttpResponseMessage response = await this.SendInputRequest("getproducts-storedprocedure", cost.ToString());

            // Verify result
            string expectedResponse = JsonConvert.SerializeObject(products);
            string actualResponse = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedResponse, actualResponse.Trim().Replace(" ", "").Replace(Environment.NewLine, ""), StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async void GetProductsByNameTest()
        {
            this.StartFunctionHost("GetProductsByName", false, this.workingDirectoryFolder);

            // Insert one row of data into Product table
            Product[] products = GetProductsWithSameName(1, "same");
            this.InsertProducts(products);

            // Run the function that queries from the ProductName view
            HttpResponseMessage response = await this.SendInputRequest("getproducts-namesview", "same");

            // Verify result
            string expectedResponse = JsonConvert.SerializeObject(products);
            string actualResponse = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedResponse, actualResponse.Trim().Replace(" ", "").Replace(Environment.NewLine, ""), StringComparer.OrdinalIgnoreCase);
        }
        private static Product[] GetProductsWithSameCost(int n, int cost)
        {
            var result = new Product[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = new Product
                {
                    ProductID = i,
                    Name = "jsTest",
                    Cost = cost
                };
            }
            return result;
        }

        private static Product[] GetProductsWithSameName(int n, string name)
        {
            var result = new Product[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = new Product
                {
                    ProductID = i,
                    Name = name,
                    Cost = i
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
