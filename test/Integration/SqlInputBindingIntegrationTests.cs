// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class SqlInputBindingIntegrationTests : IntegrationTestBase
    {
        public SqlInputBindingIntegrationTests(ITestOutputHelper output) : base(output)
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
            this.StartFunctionHost(nameof(GetProducts));

            // Generate T-SQL to insert n rows of data with cost
            Product[] products = GetProductsWithSameCost(n, cost);
            this.InsertProducts(products);

            // Run the function
            HttpResponseMessage response = await this.SendInputRequest("getproducts", cost.ToString());

            // Verify result
            string expectedResponse = JsonConvert.SerializeObject(products);
            string actualResponse = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedResponse, actualResponse, StringComparer.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(0, 99)]
        [InlineData(1, -999)]
        [InlineData(100, 999)]
        public async void GetProductsStoredProcedureTest(int n, int cost)
        {
            this.StartFunctionHost(nameof(GetProductsStoredProcedure));

            // Generate T-SQL to insert n rows of data with cost
            Product[] products = GetProductsWithSameCost(n, cost);
            this.InsertProducts(products);

            // Run the function
            HttpResponseMessage response = await this.SendInputRequest("getproducts-storedprocedure", cost.ToString());

            // Verify result
            string expectedResponse = JsonConvert.SerializeObject(products);
            string actualResponse = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedResponse, actualResponse, StringComparer.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 20)]
        [InlineData(100, 1000)]
        public async void GetProductsNameEmptyTest(int n, int cost)
        {
            this.StartFunctionHost(nameof(GetProductsNameEmpty));

            // Add a bunch of noise data
            this.InsertProducts(GetProductsWithSameCost(n * 2, cost));

            // Now add the actual test data
            Product[] products = GetProductsWithSameCostAndName(n, cost, "", n * 2);
            this.InsertProducts(products);

            Assert.Equal(n, this.ExecuteScalar($"select count(1) from Products where name = '' and cost = {cost}"));

            // Run the function
            HttpResponseMessage response = await this.SendInputRequest("getproducts-nameempty", cost.ToString());

            // Verify result
            string expectedResponse = JsonConvert.SerializeObject(products);
            string actualResponse = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedResponse, actualResponse, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async void GetProductsByCostTest()
        {
            this.StartFunctionHost(nameof(GetProductsStoredProcedureFromAppSetting));

            // Generate T-SQL to insert n rows of data with cost
            Product[] products = GetProducts(3, 100);
            this.InsertProducts(products);
            Product[] productsWithCost100 = GetProducts(1, 100);

            // Run the function
            HttpResponseMessage response = await this.SendInputRequest("getproductsbycost");

            // Verify result
            string expectedResponse = JsonConvert.SerializeObject(productsWithCost100);
            string actualResponse = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedResponse, actualResponse, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async void GetProductNamesViewTest()
        {
            this.StartFunctionHost(nameof(GetProductNamesView));

            // Insert one row of data into Product table
            Product[] products = GetProductsWithSameCost(1, 100);
            this.InsertProducts(products);

            // Run the function that queries from the ProductName view
            HttpResponseMessage response = await this.SendInputRequest("getproduct-namesview");

            // Verify result
            string expectedResponse = "[{\"name\":\"test\"}]";
            string actualResponse = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedResponse, actualResponse, StringComparer.OrdinalIgnoreCase);
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

        private static Product[] GetProducts(int n, int cost)
        {
            var result = new Product[n];
            for (int i = 1; i <= n; i++)
            {
                result[i - 1] = new Product
                {
                    ProductID = i,
                    Name = "test",
                    Cost = cost * i
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
