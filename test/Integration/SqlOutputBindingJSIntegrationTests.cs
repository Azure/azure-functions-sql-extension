// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class SqlOutputBindingJSIntegrationTests : IntegrationTestBase
    {
        private readonly string workingDirectoryFolder = "samples-js";
        public SqlOutputBindingJSIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<HttpResponseMessage> SendOutputRequest(string functionName, IDictionary<string, string> query = null)
        {
            string requestUri = $"http://localhost:{this.Port}/api/{functionName}";

            if (query != null)
            {
                string jsonData = JsonConvert.SerializeObject(query);
                return await this.SendPostRequest(requestUri, jsonData);
            }

            return await this.SendGetRequest(requestUri);
        }

        [Theory]
        [InlineData(1, "Test", 5)]
        [InlineData(0, "", 0)]
        [InlineData(-500, "ABCD", 580)]
        public void AddProductTest(int id, string name, int cost)
        {
            this.StartFunctionHost("AddProduct", false, this.workingDirectoryFolder);

            string productJson = string.Format("\"productid\":{0},\"name\":\"{1}\",\"cost\":{2}", id.ToString(), name, cost.ToString());
            var query = new Dictionary<string, string>()
            {
                { "item", string.Concat("{", productJson, "}") }
            };

            this.SendOutputRequest("addproduct", query).Wait();

            // Verify result
            Assert.Equal(name, this.ExecuteScalar($"select Name from Products where ProductId={id}"));
            Assert.Equal(cost, this.ExecuteScalar($"select cost from Products where ProductId={id}"));
        }

        /// <summary>
        /// Tests that when using a table with an identity column that if the identity column is specified
        /// by the function we handle inserting/updating that correctly.
        /// </summary>
        [Fact]
        public void UpsertProductsTest()
        {
            this.StartFunctionHost("UpsertProducts", false, this.workingDirectoryFolder);

            // First insert some test data
            this.ExecuteNonQuery("INSERT INTO Products VALUES (1, 'test', 100)");
            this.ExecuteNonQuery("INSERT INTO Products VALUES (2, 'test', 100)");
            this.ExecuteNonQuery("INSERT INTO Products VALUES (3, 'test', 100)");

            this.SendOutputRequest("upsertproducts").Wait();

            // Function call changes first 2 rows to (1, 'Cup', 2) and (2, 'Glasses', 12)
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(1) FROM Products WHERE Cost = 100"));
            Assert.Equal(2, this.ExecuteScalar("SELECT Cost FROM Products WHERE ProductId = 1"));
            Assert.Equal(2, this.ExecuteScalar("SELECT ProductId FROM Products WHERE Cost = 12"));
        }


        [Fact]
        public void AddProductExtraColumnsTest()
        {
            this.StartFunctionHost("AddProductExtraColumns", false, this.workingDirectoryFolder);

            // Since ProductExtraColumns has columns that does not exist in the table,
            // no rows should be added to the table.
            Assert.Throws<AggregateException>(() => this.SendOutputRequest("addproduct-extracolumns").Wait());
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM Products"));
        }

        [Fact]
        public void AddProductMissingColumnsTest()
        {
            this.StartFunctionHost("AddProductMissingColumns", false, this.workingDirectoryFolder);

            // Even though the ProductMissingColumns object is missing the Cost column,
            // the row should still be added successfully since Cost can be null.
            this.SendOutputRequest("addproduct-missingcolumns").Wait();
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM Products"));
        }

        [Fact]
        public void AddProductMissingColumnsNotNullTest()
        {
            this.StartFunctionHost("AddProductMissingColumnsExceptionFunction", false, this.workingDirectoryFolder);

            // Since the Sql table does not allow null for the Cost column,
            // inserting a row without a Cost value should throw an Exception.
            Assert.Throws<AggregateException>(() => this.SendOutputRequest("addproduct-missingcolumnsexception").Wait());
        }

        [Fact]
        public void AddProductNoPartialUpsertTest()
        {
            this.StartFunctionHost("AddProductsNoPartialUpsert", false, this.workingDirectoryFolder);

            Assert.Throws<AggregateException>(() => this.SendOutputRequest("addproducts-nopartialupsert").Wait());
            // No rows should be upserted since there was a row with an invalid value
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsNameNotNull"));
        }

        /// <summary>
        /// Tests that for tables with an identity column we are able to insert items.
        /// </summary>
        [Fact]
        public void AddProductWithIdentity()
        {
            this.StartFunctionHost("AddProductWithIdentityColumn", false, this.workingDirectoryFolder);
            // Identity column (ProductID) is left out for new items
            string productJson = string.Format("\"name\":\"MyProduct\",\"cost\":1");
            var query = new Dictionary<string, string>()
            {
                { "item", string.Concat("{", productJson, "}") }
            };

            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            this.SendOutputRequest("AddProductWithIdentityColumn", query).Wait();
            // Product should have been inserted correctly even without an ID when there's an identity column present
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
        }

        /// <summary>
        /// Tests that for tables with multiple primary columns (including an itemtity column) we are able to
        /// insert items.
        /// </summary>
        [Fact]
        public void AddProductWithIdentity_MultiplePrimaryColumns()
        {
            this.StartFunctionHost("AddProductWithMultiplePrimaryColumnsAndIdentity", false, this.workingDirectoryFolder);
            string productJson = string.Format("\"externalid\":101,\"name\":\"MyProduct\",\"cost\":1");
            var query = new Dictionary<string, string>()
            {
                { "item", string.Concat("{", productJson, "}") }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
            this.SendOutputRequest("AddProductWithMultiplePrimaryColumnsAndIdentity", query).Wait();
            // Product should have been inserted correctly even without an ID when there's an identity column present
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
        }

        /// <summary>
        /// Tests that when using a table with an identity column we can handle a null (missing) identity column
        /// </summary>
        [Fact]
        public void AddProductWithIdentity_NoIdentityColumn()
        {
            this.StartFunctionHost("AddProductWithIdentityColumnIncluded", false, this.workingDirectoryFolder);
            // ProductId column is missing
            string productJson = string.Format("\"name\":\"MyProduct1\",\"cost\":1");
            var query = new Dictionary<string, string>()
            {
                { "item", string.Concat("{", productJson, "}") }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            this.SendOutputRequest("AddProductWithIdentityColumnIncluded", query).Wait();
            // New row should have been inserted
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            productJson = string.Format("\"name\":\"MyProduct2\",\"cost\":1");
            query = new Dictionary<string, string>()
            {
                { "item", string.Concat("{", productJson, "}") }
            };
            this.SendOutputRequest("AddProductWithIdentityColumnIncluded", query).Wait();
            // Another new row should have been inserted
            Assert.Equal(2, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
        }

        /// <summary>
        /// Tests that when using a table with an identity column along with other primary 
        /// keys an error is thrown if at least one of the primary keys is missing.
        /// </summary>
        [Fact]
        public void AddProductWithIdentity_MissingPrimaryColumn()
        {
            this.StartFunctionHost("AddProductWithMultiplePrimaryColumnsAndIdentity", false, this.workingDirectoryFolder);
            // Missing externalId
            string productJson = string.Format("\"name\":\"MyProduct\",\"cost\":1");
            var query = new Dictionary<string, string>()
            {
                { "item", string.Concat("{", productJson, "}") }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
            Assert.Throws<AggregateException>(() => this.SendOutputRequest("AddProductWithMultiplePrimaryColumnsAndIdentity", query).Wait());
            // Nothing should have been inserted
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
        }

        /// <summary>
        /// Tests that when using a case sensitive database, an error is thrown if 
        /// the POCO fields case and column names case do not match.
        /// </summary>
        [Fact]
        public void AddProductCaseSensitiveTest()
        {
            this.StartFunctionHost("AddProduct", false, this.workingDirectoryFolder);

            // Change database collation to case sensitive
            this.ExecuteNonQuery($"ALTER DATABASE {this.DatabaseName} COLLATE Latin1_General_CS_AS");

            string productJson = string.Format("\"productid\":1,\"name\":\"test\",\"cost\":100");
            var query = new Dictionary<string, string>()
            {
                { "item", string.Concat("{", productJson, "}") }
            };

            // The upsert should fail since the database is case sensitive and the column name "ProductId"
            // does not match the POCO field "ProductID"
            Assert.Throws<AggregateException>(() => this.SendOutputRequest("addproduct", query).Wait());

            // Change database collation back to case insensitive
            this.ExecuteNonQuery($"ALTER DATABASE {this.DatabaseName} COLLATE Latin1_General_CI_AS");

            this.SendOutputRequest("addproduct", query).Wait();

            // Verify result
            Assert.Equal("test", this.ExecuteScalar($"select Name from Products where ProductId={1}"));
            Assert.Equal(100, this.ExecuteScalar($"select cost from Products where ProductId={1}"));
        }
    }
}
