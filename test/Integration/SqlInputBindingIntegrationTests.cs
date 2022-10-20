// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class SqlInputBindingIntegrationTests : IntegrationTestBase
    {
        public SqlInputBindingIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }
        [Theory]
        [SqlInlineData(0, 100)]
        [SqlInlineData(1, -500)]
        [SqlInlineData(100, 500)]
        [UnsupportedLanguages(SupportedLanguages.CSharp, SupportedLanguages.JavaScript)]
        public async void GetProductsTest(int n, int cost, SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(GetProducts), lang);

            // Generate T-SQL to insert n rows of data with cost
            Product[] products = GetProductsWithSameCost(n, cost);
            this.InsertProducts(products);

            // Run the function
            HttpResponseMessage response = await this.SendInputRequest("getproducts", cost.ToString());

            // Verify result
            string expectedResponse = JsonConvert.SerializeObject(products);
            string actualResponse = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedResponse, TestUtils.CleanJsonString(actualResponse), StringComparer.OrdinalIgnoreCase);
        }

        [Theory]
        [SqlInlineData(0, 99)]
        [SqlInlineData(1, -999)]
        [SqlInlineData(100, 999)]
        [UnsupportedLanguages(SupportedLanguages.CSharp, SupportedLanguages.JavaScript)]

        public async void GetProductsStoredProcedureTest(int n, int cost, SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(GetProductsStoredProcedure), lang);

            // Generate T-SQL to insert n rows of data with cost
            Product[] products = GetProductsWithSameCost(n, cost);
            this.InsertProducts(products);

            // Run the function
            HttpResponseMessage response = await this.SendInputRequest("getproducts-storedprocedure", cost.ToString());

            // Verify result
            string expectedResponse = JsonConvert.SerializeObject(products);
            string actualResponse = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedResponse, TestUtils.CleanJsonString(actualResponse), StringComparer.OrdinalIgnoreCase);
        }

        [Theory]
        [SqlInlineData(0, 0)]
        [SqlInlineData(1, 20)]
        [SqlInlineData(100, 1000)]
        [UnsupportedLanguages(SupportedLanguages.CSharp, SupportedLanguages.JavaScript)]

        public async void GetProductsNameEmptyTest(int n, int cost, SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(GetProductsNameEmpty), lang);

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

            Assert.Equal(expectedResponse, TestUtils.CleanJsonString(actualResponse), StringComparer.OrdinalIgnoreCase);
        }

        [Theory]
        [SqlInlineData()]
        [UnsupportedLanguages(SupportedLanguages.CSharp, SupportedLanguages.JavaScript)]
        public async void GetProductsByCostTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(GetProductsStoredProcedureFromAppSetting), lang);

            // Generate T-SQL to insert n rows of data with cost
            Product[] products = GetProducts(3, 100);
            this.InsertProducts(products);
            Product[] productsWithCost100 = GetProducts(1, 100);

            // Run the function
            HttpResponseMessage response = await this.SendInputRequest("getproductsbycost");

            // Verify result
            string expectedResponse = JsonConvert.SerializeObject(productsWithCost100);
            string actualResponse = await response.Content.ReadAsStringAsync();

            Assert.Equal(expectedResponse, TestUtils.CleanJsonString(actualResponse), StringComparer.OrdinalIgnoreCase);
        }

        [Theory]
        [UnsupportedLanguages(SupportedLanguages.CSharp, SupportedLanguages.JavaScript)]
        [SqlInlineData()]
        public async void GetProductNamesViewTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(GetProductNamesView), lang);

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

        /// <summary>
        /// Verifies that serializing an item with various data types works when the language is
        /// set to a non-enUS language.
        /// </summary>
        [Theory]
        [SqlInlineData()]
        [UnsupportedLanguages(SupportedLanguages.CSharp, SupportedLanguages.JavaScript)]
        public async void GetProductsColumnTypesSerializationDifferentCultureTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(GetProductsColumnTypesSerializationDifferentCulture), lang, true);

            this.ExecuteNonQuery("INSERT INTO [dbo].[ProductsColumnTypes] VALUES (" +
                "999, " + // ProductId
                "GETDATE(), " + // Datetime field
                "GETDATE())"); // Datetime2 field

            await this.SendInputRequest("getproducts-columntypesserializationdifferentculture");

            // If we get here the test has succeeded - it'll throw an exception if serialization fails
        }

        /// <summary>
        /// Verifies that serializing an item with various data types works as expected
        /// </summary>
        [Theory]
        [SqlInlineData()]
        [UnsupportedLanguages(SupportedLanguages.CSharp, SupportedLanguages.JavaScript)]

        public async void GetProductsColumnTypesSerializationTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(GetProductsColumnTypesSerialization), lang, true);

            this.ExecuteNonQuery("INSERT INTO [dbo].[ProductsColumnTypes] VALUES (" +
                "999, " + // ProductId
                "GETDATE(), " + // Datetime field
                "GETDATE())"); // Datetime2 field

            await this.SendInputRequest("getproducts-columntypesserialization");

            // If we get here the test has succeeded - it'll throw an exception if serialization fails
        }
    }
}
