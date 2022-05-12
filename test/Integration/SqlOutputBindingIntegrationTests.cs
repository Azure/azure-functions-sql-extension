// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class SqlOutputBindingIntegrationTests : IntegrationTestBase
    {
        public SqlOutputBindingIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<HttpResponseMessage> SendOutputRequest(string functionName, IDictionary<string, string> query = null, bool asPost = false)
        {
            string requestUri = $"http://localhost:{this.Port}/api/{functionName}";

            if (asPost)
            {
                string jsonData = query != null ? JsonConvert.SerializeObject(query) : string.Empty;
                return await this.SendPostRequest(requestUri, jsonData);
            }
            else
            {
                if (query != null)
                {
                    requestUri = QueryHelpers.AddQueryString(requestUri, query);
                }
                return await this.SendGetRequest(requestUri);
            }

        }

        [Theory]
        [InlineData(1, "Test", 5)]
        [InlineData(0, "", 0)]
        [InlineData(-500, "ABCD", 580)]
        [InlineData(1, "Test", 5, "samples-js")]
        [InlineData(0, "", 0, "samples-js")]
        [InlineData(-500, "ABCD", 580, "samples-js")]
        public void AddProductTest(int id, string name, int cost, string workingDirectory = "SqlExtensionSamples")
        {
            this.StartFunctionHost(nameof(AddProduct), workingDirectory);

            var query = new Dictionary<string, string>()
            {
                { "productId", id.ToString() },
                { "name", name },
                { "cost", cost.ToString() }
            };

            this.SendOutputRequest("addproduct", query, true).Wait();

            // Verify result
            Assert.Equal(name, this.ExecuteScalar($"select Name from Products where ProductId={id}"));
            Assert.Equal(cost, this.ExecuteScalar($"select cost from Products where ProductId={id}"));
        }

        [Theory]
        [InlineData(1, "Test", 5)]
        [InlineData(0, "", 0)]
        [InlineData(-500, "ABCD", 580)]
        [InlineData(1, "Test", 5, "samples-js")]
        [InlineData(0, "null", 0, "samples-js")]
        [InlineData(-500, "ABCD", 580, "samples-js")]
        public void AddProductQueryParametersTest(int id, string name, int cost, string workingDirectory = "SqlExtensionSamples")
        {
            this.StartFunctionHost(nameof(AddProductParams), workingDirectory);

            if (workingDirectory == "SqlExtensionSamples")
            {
                var query = new Dictionary<string, string>()
                {
                    { "productId", id.ToString() },
                    { "name", name },
                    { "cost", cost.ToString() }
                };
                this.SendOutputRequest("addproduct", query).Wait();
            }
            else
            {
                string requestUri = $"http://localhost:{this.Port}/api/addproduct/{id}/{name}/{cost}";
                this.SendGetRequest(requestUri).Wait();
            }


            // Verify result
            Assert.Equal(name, this.ExecuteScalar($"select Name from Products where ProductId={id}"));
            Assert.Equal(cost, this.ExecuteScalar($"select cost from Products where ProductId={id}"));
        }

        [Theory]
        [InlineData("SqlExtensionSamples")]
        [InlineData("samples-js")]
        public void AddProductArrayTest(string workingDirectory)
        {
            this.StartFunctionHost(nameof(AddProductsArray), workingDirectory);

            // First insert some test data
            this.ExecuteNonQuery("INSERT INTO Products VALUES (1, 'test', 100)");
            this.ExecuteNonQuery("INSERT INTO Products VALUES (2, 'test', 100)");
            this.ExecuteNonQuery("INSERT INTO Products VALUES (3, 'test', 100)");

            this.SendOutputRequest("addproducts-array").Wait();

            // Function call changes first 2 rows to (1, 'Cup', 2) and (2, 'Glasses', 12)
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(1) FROM Products WHERE Cost = 100"));
            Assert.Equal(2, this.ExecuteScalar("SELECT Cost FROM Products WHERE ProductId = 1"));
            Assert.Equal(2, this.ExecuteScalar("SELECT ProductId FROM Products WHERE Cost = 12"));
        }

        [Theory]
        [InlineData("SqlExtensionSamples")]
        public void AddProductsCollectorTest(string workingDirectory)
        {
            this.StartFunctionHost(nameof(AddProductsCollector), workingDirectory);

            // Function should add 5000 rows to the table
            this.SendOutputRequest("addproducts-collector").Wait();

            Assert.Equal(5000, this.ExecuteScalar("SELECT COUNT(1) FROM Products"));
        }

        [Theory]
        [InlineData("SqlExtensionSamples")]
        public void QueueTriggerProductsTest(string workingDirectory)
        {
            this.StartFunctionHost(nameof(QueueTriggerProducts), workingDirectory);

            string uri = $"http://localhost:{this.Port}/admin/functions/QueueTriggerProducts";
            string json = "{ 'input': 'Test Data' }";

            this.SendPostRequest(uri, json).Wait();

            Thread.Sleep(5000);

            // Function should add 100 rows
            Assert.Equal(100, this.ExecuteScalar("SELECT COUNT(1) FROM Products"));
        }

        [Fact]
        public void TimerTriggerProductsTest()
        {
            this.StartFunctionHost(nameof(TimerTriggerProducts), "SqlExtensionSamples");

            // Since this function runs on a schedule (every 5 seconds), we don't need to invoke it.
            // We will wait 6 seconds to guarantee that it has been fired at least once, and check that at least 1000 rows of data has been added.
            Thread.Sleep(6000);

            int rowsAdded = (int)this.ExecuteScalar("SELECT COUNT(1) FROM Products");
            Assert.True(rowsAdded >= 1000);
        }

        [Theory]
        [InlineData("SqlExtensionSamples", true)]
        [InlineData("samples-js", false)]
        public void AddProductExtraColumnsTest(string workingDirectory, bool useTestFolder)
        {
            this.StartFunctionHost(nameof(AddProductExtraColumns), workingDirectory, useTestFolder);

            // Since ProductExtraColumns has columns that does not exist in the table,
            // no rows should be added to the table.
            Assert.Throws<AggregateException>(() => this.SendOutputRequest("addproduct-extracolumns").Wait());
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM Products"));
        }

        [Theory]
        [InlineData("SqlExtensionSamples", true)]
        [InlineData("samples-js", false)]
        public void AddProductMissingColumnsTest(string workingDirectory, bool useTestFolder)
        {
            this.StartFunctionHost(nameof(AddProductMissingColumns), workingDirectory, useTestFolder);

            // Even though the ProductMissingColumns object is missing the Cost column,
            // the row should still be added successfully since Cost can be null.
            this.SendOutputRequest("addproduct-missingcolumns").Wait();
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM Products"));
        }

        [Theory]
        [InlineData("SqlExtensionSamples", true)]
        [InlineData("samples-js", false)]
        public void AddProductMissingColumnsNotNullTest(string workingDirectory, bool useTestFolder)
        {
            this.StartFunctionHost(nameof(AddProductMissingColumnsExceptionFunction), workingDirectory, useTestFolder);

            // Since the Sql table does not allow null for the Cost column,
            // inserting a row without a Cost value should throw an Exception.
            Assert.Throws<AggregateException>(() => this.SendOutputRequest("addproduct-missingcolumnsexception").Wait());
        }

        [Theory]
        [InlineData("SqlExtensionSamples", true)]
        [InlineData("samples-js", false)]
        public void AddProductNoPartialUpsertTest(string workingDirectory, bool useTestFolder)
        {
            this.StartFunctionHost(nameof(AddProductsNoPartialUpsert), workingDirectory, useTestFolder);

            Assert.Throws<AggregateException>(() => this.SendOutputRequest("addproducts-nopartialupsert").Wait());
            // No rows should be upserted since there was a row with an invalid value
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsNameNotNull"));
        }

        /// <summary>
        /// Tests that for tables with an identity column we are able to insert items.
        /// </summary>
        [Theory]
        [InlineData("SqlExtensionSamples")]
        [InlineData("samples-js", true)]
        public void AddProductWithIdentity(string workingDirectory, bool asPost = false)
        {
            this.StartFunctionHost(nameof(AddProductWithIdentityColumn), workingDirectory);
            // Identity column (ProductID) is left out for new items
            var query = new Dictionary<string, string>()
            {
                { "name", "MyProduct" },
                { "cost", "1" }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            this.SendOutputRequest(nameof(AddProductWithIdentityColumn), query, asPost).Wait();
            // Product should have been inserted correctly even without an ID when there's an identity column present
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
        }

        /// <summary>
        /// Tests that for tables with multiple primary columns (including an itemtity column) we are able to
        /// insert items.
        /// </summary>
        [Theory]
        [InlineData("SqlExtensionSamples")]
        [InlineData("samples-js", true)]
        public void AddProductWithIdentity_MultiplePrimaryColumns(string workingDirectory, bool asPost = false)
        {
            this.StartFunctionHost(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity), workingDirectory);
            var query = new Dictionary<string, string>()
            {
                { "externalId", "101" },
                { "name", "MyProduct" },
                { "cost", "1" }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
            this.SendOutputRequest(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity), query, asPost).Wait();
            // Product should have been inserted correctly even without an ID when there's an identity column present
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
        }

        /// <summary>
        /// Tests that when using a table with an identity column that if the identity column is specified
        /// by the function we handle inserting/updating that correctly.
        /// </summary>
        [Theory]
        [InlineData("SqlExtensionSamples")]
        [InlineData("samples-js", true)]
        public void AddProductWithIdentity_SpecifyIdentityColumn(string workingDirectory, bool asPost = false)
        {
            this.StartFunctionHost(nameof(AddProductWithIdentityColumnIncluded), workingDirectory);
            var query = new Dictionary<string, string>()
            {
                { "productId", "1" },
                { "name", "MyProduct" },
                { "cost", "1" }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            this.SendOutputRequest(nameof(AddProductWithIdentityColumnIncluded), query, asPost).Wait();
            // New row should have been inserted
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            query = new Dictionary<string, string>()
            {
                { "productId", "1" },
                { "name", "MyProduct2" },
                { "cost", "1" }
            };
            this.SendOutputRequest(nameof(AddProductWithIdentityColumnIncluded), query, asPost).Wait();
            // Existing row should have been updated
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity WHERE Name='MyProduct2'"));
        }

        /// <summary>
        /// Tests that when using a table with an identity column we can handle a null (missing) identity column
        /// </summary>
        [Theory]
        [InlineData("SqlExtensionSamples")]
        [InlineData("samples-js")]
        public void AddProductWithIdentity_NoIdentityColumn(string workingDirectory)
        {
            this.StartFunctionHost(nameof(AddProductWithIdentityColumnIncluded), workingDirectory);
            var query = new Dictionary<string, string>()
            {
                { "name", "MyProduct" },
                { "cost", "1" }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            this.SendOutputRequest(nameof(AddProductWithIdentityColumnIncluded), query).Wait();
            // New row should have been inserted
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            query = new Dictionary<string, string>()
            {
                { "name", "MyProduct2" },
                { "cost", "1" }
            };
            this.SendOutputRequest(nameof(AddProductWithIdentityColumnIncluded), query).Wait();
            // Another new row should have been inserted
            Assert.Equal(2, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
        }

        /// <summary>
        /// Tests that when using a table with an identity column along with other primary 
        /// keys an error is thrown if at least one of the primary keys is missing.
        /// </summary>
        [Theory]
        [InlineData("SqlExtensionSamples")]
        [InlineData("samples-js", true)]
        public void AddProductWithIdentity_MissingPrimaryColumn(string workingDirectory, bool asPost = false)
        {
            this.StartFunctionHost(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity), workingDirectory);
            var query = new Dictionary<string, string>()
            {
                // Missing externalId
                { "name", "MyProduct" },
                { "cost", "1" }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
            Assert.Throws<AggregateException>(() => this.SendOutputRequest(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity), query, asPost).Wait());
            // Nothing should have been inserted
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
        }

        /// <summary>
        /// Tests that when using a case sensitive database, an error is thrown if 
        /// the POCO fields case and column names case do not match.
        /// </summary>
        [Theory]
        [InlineData("SqlExtensionSamples", true)]
        [InlineData("samples-js", true)]
        public void AddProductCaseSensitiveTest(string workingDirectory, bool asPost = false)
        {
            this.StartFunctionHost(nameof(AddProduct), workingDirectory);

            // Change database collation to case sensitive
            this.ExecuteNonQuery($"ALTER DATABASE {this.DatabaseName} COLLATE Latin1_General_CS_AS");

            var query = new Dictionary<string, string>()
            {
                { "productid", "1" },
                { "name", "test" },
                { "cost", "100" }
            };

            // The upsert should fail since the database is case sensitive and the column name "ProductId"
            // does not match the POCO field "ProductID"
            Assert.Throws<AggregateException>(() => this.SendOutputRequest(nameof(AddProduct), query, asPost).Wait());

            // Change database collation back to case insensitive
            this.ExecuteNonQuery($"ALTER DATABASE {this.DatabaseName} COLLATE Latin1_General_CI_AS");

            this.SendOutputRequest(nameof(AddProduct), query, asPost).Wait();

            // Verify result
            Assert.Equal("test", this.ExecuteScalar($"select Name from Products where ProductId={1}"));
            Assert.Equal(100, this.ExecuteScalar($"select cost from Products where ProductId={1}"));
        }
    }
}
