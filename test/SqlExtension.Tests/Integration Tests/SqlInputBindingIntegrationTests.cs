// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SqlExtensionSamples;
using Xunit;
using Xunit.Abstractions;

namespace SqlExtension.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class SqlInputBindingIntegrationTests : IntegrationTestBase
    {
        public SqlInputBindingIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<HttpResponseMessage> SendInputRequest(string functionName, string query = "")
        {
            string requestUri = $"http://localhost:{Port}/api/{functionName}/{query}";

            return await SendGetRequest(requestUri);
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(1, -500)]
        [InlineData(100, 500)]
        public async void GetProductsTest(int n, int cost)
        {
            // Generate T-SQL to insert n rows of data with cost
            Product[] products = GetProductsWithSameCost(n, cost);
            InsertProducts(products);
            
            // Run the function
            HttpResponseMessage response = await SendInputRequest("getproducts", cost.ToString());

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
            // Generate T-SQL to insert n rows of data with cost
            Product[] products = GetProductsWithSameCost(n, cost);
            InsertProducts(products);

            // Run the function
            HttpResponseMessage response = await SendInputRequest("getproducts-storedprocedure", cost.ToString());

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
            // Add a bunch of noise data
            InsertProducts(GetProductsWithSameCost(n * 2, cost));

            // Now add the actual test data
            Product[] products = GetProductsWithSameCostAndName(n, cost, "", n * 2);
            InsertProducts(products);

            Assert.Equal(n, ExecuteScalar($"select count(1) from Products where name = '' and cost = {cost}"));

            // Run the function
            HttpResponseMessage response = await SendInputRequest("getproducts-nameempty", cost.ToString());

            // Verify result
            string expectedResponse = JsonConvert.SerializeObject(products);
            string actualResponse = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedResponse, actualResponse, StringComparer.OrdinalIgnoreCase);
        }

        private Product[] GetProductsWithSameCost(int n, int cost)
        {
            Product[] result = new Product[n];
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

        private Product[] GetProductsWithSameCostAndName(int n, int cost, string name, int offset = 0)
        {
            Product[] result = new Product[n];
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

            StringBuilder queryBuilder = new StringBuilder();
            foreach (Product p in products)
            {
                queryBuilder.AppendLine($"INSERT INTO dbo.Products VALUES({p.ProductID}, '{p.Name}', {p.Cost});");
            }

            ExecuteNonQuery(queryBuilder.ToString());
        }
    }
}
