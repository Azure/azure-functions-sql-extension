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
    [Collection(IntegrationTestsCollection.Name)]
    [LogTestName]
    public class SqlInputBindingIntegrationTests : IntegrationTestBase
    {
        public SqlInputBindingIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [SqlInlineData(0, 100)]
        [SqlInlineData(1, -500)]
        [SqlInlineData(100, 500)]
        public async void GetProductsTest(int n, int cost, SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(GetProducts), lang);

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
        [SqlInlineData(0, 99)]
        [SqlInlineData(1, -999)]
        [SqlInlineData(100, 999)]
        public async void GetProductsStoredProcedureTest(int n, int cost, SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(GetProductsStoredProcedure), lang);

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
        [SqlInlineData(0, 0)]
        [SqlInlineData(1, 20)]
        [SqlInlineData(100, 1000)]
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
            string actualResponse = await response.Content.ReadAsStringAsync();
            Product[] actualProductResponse = JsonConvert.DeserializeObject<Product[]>(actualResponse);

            Assert.Equal(products, actualProductResponse);
        }

        [Theory]
        [SqlInlineData()]
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
            string actualResponse = await response.Content.ReadAsStringAsync();
            Product[] actualProductResponse = JsonConvert.DeserializeObject<Product[]>(actualResponse);

            Assert.Equal(productsWithCost100, actualProductResponse);
        }

        [Theory]
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
        /// Verifies that serializing an item with various data types and different cultures works when using IAsyncEnumerable
        /// </summary>
        [Theory]
        [SqlInlineData("en-US")]
        [SqlInlineData("it-IT")]
        [UnsupportedLanguages(SupportedLanguages.JavaScript, SupportedLanguages.PowerShell, SupportedLanguages.Java, SupportedLanguages.Python)] // IAsyncEnumerable is only available in C#
        public async void GetProductsColumnTypesSerializationAsyncEnumerableTest(string culture, SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(GetProductsColumnTypesSerializationAsyncEnumerable), lang, true);

            string datetime = "2022-10-20 12:39:13.123";
            ProductColumnTypes[] expectedResponse = JsonConvert.DeserializeObject<ProductColumnTypes[]>("[{\"ProductId\":999,\"BigInt\":999,\"Bit\":true,\"DecimalType\":1.2345,\"Money\":1.2345,\"Numeric\":1.2345,\"SmallInt\":1,\"SmallMoney\":1.2345,\"TinyInt\":1,\"FloatType\":0.1,\"Real\":0.1,\"Date\":\"2022-10-20T00:00:00.000Z\",\"Datetime\":\"2022-10-20T12:39:13.123Z\",\"Datetime2\":\"2022-10-20T12:39:13.123Z\",\"DatetimeOffset\":\"2022-10-20T12:39:13.123Z\",\"SmallDatetime\":\"2022-10-20T12:39:00.000Z\",\"Time\":\"12:39:13.1230000\",\"CharType\":\"test\",\"Varchar\":\"test\",\"Nchar\":\"\uFFFD\u0020\u0020\u0020\",\"Nvarchar\":\"\uFFFD\",\"Binary\":\"dGVzdA==\",\"Varbinary\":\"dGVzdA==\"}]");

            this.ExecuteNonQuery("INSERT INTO [dbo].[ProductsColumnTypes] VALUES (" +
                "999, " + // ProductId,
                "999, " + // BigInt
                "1, " + // Bit
                "1.2345, " + // DecimalType
                "1.2345, " + // Money
                "1.2345, " + // Numeric
                "1, " + // SmallInt
                "1.2345, " + // SmallMoney
                "1, " + // TinyInt
                ".1, " + // FloatType
                ".1, " + // Real
                $"CONVERT(DATE, '{datetime}'), " + // Date
                $"CONVERT(DATETIME, '{datetime}'), " + // Datetime
                $"CONVERT(DATETIME2, '{datetime}'), " + // Datetime2
                $"CONVERT(DATETIMEOFFSET, '{datetime}'), " + // DatetimeOffset
                $"CONVERT(SMALLDATETIME, '{datetime}'), " + // SmallDatetime
                $"CONVERT(TIME, '{datetime}'), " + // Time
                "'test', " + // CharType
                "'test', " + // Varchar
                "NCHAR(0xD84C), " + // Nchar
                "NCHAR(0xD84C), " +  // Nvarchar
                "CONVERT(BINARY, 'test'), " + // Binary
                "CONVERT(VARBINARY, 'test'))"); // Varbinary

            HttpResponseMessage response = await this.SendInputRequest("getproducts-columntypesserializationasyncenumerable", $"?culture={culture}");
            // We expect the datetime and datetime2 fields to be returned in UTC format
            string actualResponse = await response.Content.ReadAsStringAsync();
            ProductColumnTypes[] actualProductResponse = JsonConvert.DeserializeObject<ProductColumnTypes[]>(actualResponse);
            Assert.Equal(expectedResponse, actualProductResponse);
        }

        /// <summary>
        /// Verifies that serializing an item with various data types works as expected
        /// </summary>
        [Theory]
        [SqlInlineData()]
        public async void GetProductsColumnTypesSerializationTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(GetProductsColumnTypesSerialization), lang, true);

            string datetime = "2022-10-20 12:39:13.123";
            this.ExecuteNonQuery("INSERT INTO [dbo].[ProductsColumnTypes] VALUES (" +
                "999, " + // ProductId,
                "999, " + // BigInt
                "1, " + // Bit
                "1.2345, " + // DecimalType
                "1.2345, " + // Money
                "1.2345, " + // Numeric
                "1, " + // SmallInt
                "1.2345, " + // SmallMoney
                "1, " + // TinyInt
                ".1, " + // FloatType
                ".1, " + // Real
                $"CONVERT(DATE, '{datetime}'), " + // Date
                $"CONVERT(DATETIME, '{datetime}'), " + // Datetime
                $"CONVERT(DATETIME2, '{datetime}'), " + // Datetime2
                $"CONVERT(DATETIMEOFFSET, '{datetime}'), " + // DatetimeOffset
                $"CONVERT(SMALLDATETIME, '{datetime}'), " + // SmallDatetime
                $"CONVERT(TIME, '{datetime}'), " + // Time
                "'test', " + // CharType
                "'test', " + // Varchar
                "NCHAR(0xD84C), " + // Nchar
                "NCHAR(0xD84C), " +  // Nvarchar
                "CONVERT(BINARY, 'test'), " + // Binary
                "CONVERT(VARBINARY, 'test'))"); // Varbinary

            HttpResponseMessage response = await this.SendInputRequest("getproducts-columntypesserialization");
            // We expect the date fields to be returned in UTC format
            ProductColumnTypes[] expectedResponse = JsonConvert.DeserializeObject<ProductColumnTypes[]>("[{\"ProductId\":999,\"BigInt\":999,\"Bit\":true,\"DecimalType\":1.2345,\"Money\":1.2345,\"Numeric\":1.2345,\"SmallInt\":1,\"SmallMoney\":1.2345,\"TinyInt\":1,\"FloatType\":0.1,\"Real\":0.1,\"Date\":\"2022-10-20T00:00:00.000Z\",\"Datetime\":\"2022-10-20T12:39:13.123Z\",\"Datetime2\":\"2022-10-20T12:39:13.123Z\",\"DatetimeOffset\":\"2022-10-20T12:39:13.123Z\",\"SmallDatetime\":\"2022-10-20T12:39:00.000Z\",\"Time\":\"12:39:13.1230000\",\"CharType\":\"test\",\"Varchar\":\"test\",\"Nchar\":\"\uFFFD\u0020\u0020\u0020\",\"Nvarchar\":\"\uFFFD\",\"Binary\":\"dGVzdA==\",\"Varbinary\":\"dGVzdA==\"}]");
            string actualResponse = await response.Content.ReadAsStringAsync();
            ProductColumnTypes[] actualProductResponse = JsonConvert.DeserializeObject<ProductColumnTypes[]>(actualResponse);
            Assert.Equal(expectedResponse, actualProductResponse);
        }

        /// <summary>
        /// Tests that querying from a case sensitive database works correctly.
        /// </summary>
        [Theory]
        [SqlInlineData()]
        public async void GetProductsFromCaseSensitiveDatabase(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(GetProducts), lang);

            // Change database collation to case sensitive
            this.ExecuteNonQuery($"ALTER DATABASE {this.DatabaseName} SET Single_User WITH ROLLBACK IMMEDIATE; ALTER DATABASE {this.DatabaseName} COLLATE Latin1_General_CS_AS; ALTER DATABASE {this.DatabaseName} SET Multi_User;");

            // Generate T-SQL to insert 10 rows of data with cost 100
            Product[] products = GetProductsWithSameCost(10, 100);
            this.InsertProducts(products);

            // Run the function
            HttpResponseMessage response = await this.SendInputRequest("getproducts", "100");

            // Verify result
            string actualResponse = await response.Content.ReadAsStringAsync();
            Product[] actualProductResponse = JsonConvert.DeserializeObject<Product[]>(actualResponse);

            Assert.Equal(products, actualProductResponse);

            // Change database collation back to case insensitive
            this.ExecuteNonQuery($"ALTER DATABASE {this.DatabaseName} SET Single_User WITH ROLLBACK IMMEDIATE; ALTER DATABASE {this.DatabaseName} COLLATE Latin1_General_CI_AS; ALTER DATABASE {this.DatabaseName} SET Multi_User;");
        }
    }
}
