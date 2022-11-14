// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.InputBindingSamples;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Azure.Functions.Worker.Sql.Tests.Common;
using System.Threading.Tasks;

namespace Microsoft.Azure.Functions.Worker.Sql.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class SqlInputBindingIntegrationTests : IntegrationTestBase
    {
        public SqlInputBindingIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(1, -500)]
        [InlineData(100, 500)]
        public async Task GetProductsTest(int n, int cost)
        {
            this.StartFunctionHost(nameof(GetProducts));

            // Generate T-SQL to insert n rows of data with cost
            Product[] products = GetProductsWithSameCost(n, cost);
            this.InsertProducts(products);

            // Run the function
            HttpResponseMessage response = await this.SendInputRequest("getproducts", cost.ToString());

            // Verify result
            string actualResponse = await response.Content.ReadAsStringAsync();
            Product[] actualProductResponse = JsonConvert.DeserializeObject<Product[]>(actualResponse);

            Assert.Equal(products, actualProductResponse);
        }

        [Theory]
        [InlineData(0, 99)]
        [InlineData(1, -999)]
        [InlineData(100, 999)]
        public async Task GetProductsStoredProcedureTest(int n, int cost)
        {
            this.StartFunctionHost(nameof(GetProductsStoredProcedure));

            // Generate T-SQL to insert n rows of data with cost
            Product[] products = GetProductsWithSameCost(n, cost);
            this.InsertProducts(products);

            // Run the function
            HttpResponseMessage response = await this.SendInputRequest("getproducts-storedprocedure", cost.ToString());

            // Verify result
            string actualResponse = await response.Content.ReadAsStringAsync();
            Product[] actualProductResponse = JsonConvert.DeserializeObject<Product[]>(actualResponse);

            Assert.Equal(products, actualProductResponse);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 20)]
        [InlineData(100, 1000)]
        public async Task GetProductsNameEmptyTest(int n, int cost)
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
            string actualResponse = await response.Content.ReadAsStringAsync();
            Product[] actualProductResponse = JsonConvert.DeserializeObject<Product[]>(actualResponse);

            Assert.Equal(products, actualProductResponse);
        }

        [Fact]
        public async Task GetProductsByCostTest()
        {
            this.StartFunctionHost(nameof(GetProductsStoredProcedureFromAppSetting));

            // Generate T-SQL to insert n rows of data with cost
            Product[] products = GetProducts(3, 100);
            this.InsertProducts(products);
            Product[] productsWithCost100 = GetProducts(1, 100);

            // Run the function
            HttpResponseMessage response = await this.SendInputRequest("getproductsbycost");

            // Verify result
            string actualResponse = await response.Content.ReadAsStringAsync();
            Product[] actualProductResponse = JsonConvert.DeserializeObject<Product[]>(actualResponse);

            Assert.Equal(productsWithCost100, actualProductResponse);
        }

        [Fact]
        public async Task GetProductNamesViewTest()
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

            Assert.Equal(expectedResponse, TestUtils.CleanJsonString(actualResponse), StringComparer.OrdinalIgnoreCase);
        }
    }
}
