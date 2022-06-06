// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Xunit;
using Xunit.Abstractions;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class SqlOutputBindingIntegrationTests : IntegrationTestBase
    {
        public SqlOutputBindingIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }

        private Task<HttpResponseMessage> SendOutputGetRequest(string functionName, IDictionary<string, string> query = null)
        {
            string requestUri = $"http://localhost:{this.Port}/api/{functionName}";

            if (query != null)
            {
                requestUri = QueryHelpers.AddQueryString(requestUri, query);
            }

            return this.SendGetRequest(requestUri);
        }

        private Task<HttpResponseMessage> SendOutputPostRequest(string functionName, string query)
        {
            string requestUri = $"http://localhost:{this.Port}/api/{functionName}";

            return this.SendPostRequest(requestUri, query);
        }

        [Theory]
        [SupportedLanguages(1, "Test", 5)]
        [SupportedLanguages(0, "", 0)]
        [SupportedLanguages(-500, "ABCD", 580)]
        public void AddProductTest(int id, string name, int cost, string lang)
        {
            this.StartFunctionHost(nameof(AddProduct), lang);

            var query = new Dictionary<string, string>()
            {
                { "productId", id.ToString() },
                { "name", name },
                { "cost", cost.ToString() }
            };

            this.SendOutputPostRequest("addproduct", JsonConvert.SerializeObject(query)).Wait();

            // Verify result
            Assert.Equal(name, this.ExecuteScalar($"select Name from Products where ProductId={id}"));
            Assert.Equal(cost, this.ExecuteScalar($"select cost from Products where ProductId={id}"));
        }

        [Theory]
        [SupportedLanguages(1, "Test", 5)]
        [SupportedLanguages(0, "", 0)]
        [SupportedLanguages(-500, "ABCD", 580)]
        public void AddProductParamsTest(int id, string name, int cost, string lang)
        {
            this.StartFunctionHost(nameof(AddProductParams), lang);

            var query = new Dictionary<string, string>()
            {
                { "productId", id.ToString() },
                { "name", name },
                { "cost", cost.ToString() }
            };

            this.SendOutputGetRequest("addproduct-params", query).Wait();

            // Verify result
            Assert.Equal(name, this.ExecuteScalar($"select Name from Products where ProductId={id}"));
            Assert.Equal(cost, this.ExecuteScalar($"select cost from Products where ProductId={id}"));
        }

        [Theory]
        [SupportedLanguages()]
        public void AddProductArrayTest(string lang)
        {
            this.StartFunctionHost(nameof(AddProductsArray), lang);

            // First insert some test data
            this.ExecuteNonQuery("INSERT INTO Products VALUES (1, 'test', 100)");
            this.ExecuteNonQuery("INSERT INTO Products VALUES (2, 'test', 100)");
            this.ExecuteNonQuery("INSERT INTO Products VALUES (3, 'test', 100)");

            Product[] prods = new[]
            {
                new Product()
                {
                    ProductID = 1,
                    Name = "Cup",
                    Cost = 2
                },
                new Product
                {
                    ProductID = 2,
                    Name = "Glasses",
                    Cost = 12
                }
            };

            this.SendOutputPostRequest("addproducts-array", JsonConvert.SerializeObject(prods)).Wait();

            // Function call changes first 2 rows to (1, 'Cup', 2) and (2, 'Glasses', 12)
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(1) FROM Products WHERE Cost = 100"));
            Assert.Equal(2, this.ExecuteScalar("SELECT Cost FROM Products WHERE ProductId = 1"));
            Assert.Equal(2, this.ExecuteScalar("SELECT ProductId FROM Products WHERE Cost = 12"));
        }

        [Theory]
        [SupportedLanguages()]
        public void AddProductsCollectorTest(string lang)
        {
            this.StartFunctionHost(nameof(AddProductsCollector), lang);

            // Function should add 5000 rows to the table
            this.SendOutputGetRequest("addproducts-collector").Wait();

            Assert.Equal(5000, this.ExecuteScalar("SELECT COUNT(1) FROM Products"));
        }

        [Theory]
        [SupportedLanguages()]
        public void QueueTriggerProductsTest(string lang)
        {
            this.StartFunctionHost(nameof(QueueTriggerProducts), lang);

            string uri = $"http://localhost:{this.Port}/admin/functions/QueueTriggerProducts";
            string json = "{ 'input': 'Test Data' }";

            this.SendPostRequest(uri, json).Wait();

            Thread.Sleep(5000);

            // Function should add 100 rows
            Assert.Equal(100, this.ExecuteScalar("SELECT COUNT(1) FROM Products"));
        }

        [Theory]
        [SupportedLanguages()]
        public void TimerTriggerProductsTest(string lang)
        {
            this.StartFunctionHost(nameof(TimerTriggerProducts), lang);

            // Since this function runs on a schedule (every 5 seconds), we don't need to invoke it.
            // We will wait 6 seconds to guarantee that it has been fired at least once, and check that at least 1000 rows of data has been added.
            Thread.Sleep(6000);

            int rowsAdded = (int)this.ExecuteScalar("SELECT COUNT(1) FROM Products");
            Assert.True(rowsAdded >= 1000);
        }

        [Theory]
        [SupportedLanguages()]
        public void AddProductExtraColumnsTest(string lang)
        {
            this.StartFunctionHost(nameof(AddProductExtraColumns), lang, true);

            // Since ProductExtraColumns has columns that does not exist in the table,
            // no rows should be added to the table.
            Assert.Throws<AggregateException>(() => this.SendOutputGetRequest("addproduct-extracolumns").Wait());
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM Products"));
        }

        [Theory]
        [SupportedLanguages()]
        public void AddProductMissingColumnsTest(string lang)
        {
            this.StartFunctionHost(nameof(AddProductMissingColumns), lang, true);

            // Even though the ProductMissingColumns object is missing the Cost column,
            // the row should still be added successfully since Cost can be null.
            this.SendOutputGetRequest("addproduct-missingcolumns").Wait();
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM Products"));
        }

        [Theory]
        [SupportedLanguages()]
        public void AddProductMissingColumnsNotNullTest(string lang)
        {
            this.StartFunctionHost(nameof(AddProductMissingColumnsExceptionFunction), lang, true);

            // Since the Sql table does not allow null for the Cost column,
            // inserting a row without a Cost value should throw an Exception.
            Assert.Throws<AggregateException>(() => this.SendOutputGetRequest("addproduct-missingcolumnsexception").Wait());
        }

        [Theory]
        [SupportedLanguages()]
        public void AddProductNoPartialUpsertTest(string lang)
        {
            this.StartFunctionHost(nameof(AddProductsNoPartialUpsert), lang, true);

            Assert.Throws<AggregateException>(() => this.SendOutputGetRequest("addproducts-nopartialupsert").Wait());
            // No rows should be upserted since there was a row with an invalid value
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsNameNotNull"));
        }

        /// <summary>
        /// Tests that for tables with an identity column we are able to insert items.
        /// </summary>
        [Theory]
        [SupportedLanguages()]
        public void AddProductWithIdentity(string lang)
        {
            this.StartFunctionHost(nameof(AddProductWithIdentityColumn), lang);
            // Identity column (ProductID) is left out for new items
            var query = new Dictionary<string, string>()
            {
                { "name", "MyProduct" },
                { "cost", "1" }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            this.SendOutputGetRequest(nameof(AddProductWithIdentityColumn), query).Wait();
            // Product should have been inserted correctly even without an ID when there's an identity column present
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
        }

        /// <summary>
        /// Tests that for tables with an identity column we are able to insert multiple items at once
        /// </summary>
        [Theory]
        [SupportedLanguages()]
        public void AddProductsWithIdentityColumnArray(string lang)
        {
            this.StartFunctionHost(nameof(AddProductsWithIdentityColumnArray), lang);
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            this.SendOutputGetRequest(nameof(AddProductsWithIdentityColumnArray)).Wait();
            // Multiple items should have been inserted
            Assert.Equal(2, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
        }

        /// <summary>
        /// Tests that for tables with multiple primary columns (including an itemtity column) we are able to
        /// insert items.
        /// </summary>
        [Theory]
        [SupportedLanguages()]
        public void AddProductWithIdentity_MultiplePrimaryColumns(string lang)
        {
            this.StartFunctionHost(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity), lang);
            var query = new Dictionary<string, string>()
            {
                { "externalId", "101" },
                { "name", "MyProduct" },
                { "cost", "1" }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
            this.SendOutputGetRequest(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity), query).Wait();
            // Product should have been inserted correctly even without an ID when there's an identity column present
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
        }

        /// <summary>
        /// Tests that when using a table with an identity column that if the identity column is specified
        /// by the function we handle inserting/updating that correctly.
        /// </summary>
        [Theory]
        [SupportedLanguages()]
        public void AddProductWithIdentity_SpecifyIdentityColumn(string lang)
        {
            this.StartFunctionHost(nameof(AddProductWithIdentityColumnIncluded), lang);
            var query = new Dictionary<string, string>()
            {
                { "productId", "1" },
                { "name", "MyProduct" },
                { "cost", "1" }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            this.SendOutputGetRequest(nameof(AddProductWithIdentityColumnIncluded), query).Wait();
            // New row should have been inserted
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            query = new Dictionary<string, string>()
            {
                { "productId", "1" },
                { "name", "MyProduct2" },
                { "cost", "1" }
            };
            this.SendOutputGetRequest(nameof(AddProductWithIdentityColumnIncluded), query).Wait();
            // Existing row should have been updated
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity WHERE Name='MyProduct2'"));
        }

        /// <summary>
        /// Tests that when using a table with an identity column we can handle a null (missing) identity column
        /// </summary>
        [Theory]
        [SupportedLanguages()]
        public void AddProductWithIdentity_NoIdentityColumn(string lang)
        {
            this.StartFunctionHost(nameof(AddProductWithIdentityColumnIncluded), lang);
            var query = new Dictionary<string, string>()
            {
                { "name", "MyProduct" },
                { "cost", "1" }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            this.SendOutputGetRequest(nameof(AddProductWithIdentityColumnIncluded), query).Wait();
            // New row should have been inserted
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            query = new Dictionary<string, string>()
            {
                { "name", "MyProduct2" },
                { "cost", "1" }
            };
            this.SendOutputGetRequest(nameof(AddProductWithIdentityColumnIncluded), query).Wait();
            // Another new row should have been inserted
            Assert.Equal(2, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
        }

        /// <summary>
        /// Tests that when using a table with an identity column along with other primary 
        /// keys an error is thrown if at least one of the primary keys is missing.
        /// </summary>
        [Theory]
        [SupportedLanguages()]
        public void AddProductWithIdentity_MissingPrimaryColumn(string lang)
        {
            this.StartFunctionHost(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity), lang);
            var query = new Dictionary<string, string>()
            {
                // Missing externalId
                { "name", "MyProduct" },
                { "cost", "1" }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
            Assert.Throws<AggregateException>(() => this.SendOutputGetRequest(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity), query).Wait());
            // Nothing should have been inserted
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
        }

        /// <summary>
        /// Tests that when using a case sensitive database, an error is thrown if 
        /// the POCO fields case and column names case do not match.
        /// </summary>
        [Theory]
        [SupportedLanguages()]
        public void AddProductCaseSensitiveTest(string lang)
        {
            this.StartFunctionHost(nameof(AddProductParams), lang);

            // Change database collation to case sensitive
            this.ExecuteNonQuery($"ALTER DATABASE {this.DatabaseName} COLLATE Latin1_General_CS_AS");

            var query = new Dictionary<string, string>()
            {
                { "productId", "1" },
                { "name", "test" },
                { "cost", "100" }
            };

            // The upsert should fail since the database is case sensitive and the column name "ProductId"
            // does not match the POCO field "ProductID"
            Assert.Throws<AggregateException>(() => this.SendOutputGetRequest("addproduct-params", query).Wait());

            // Change database collation back to case insensitive
            this.ExecuteNonQuery($"ALTER DATABASE {this.DatabaseName} COLLATE Latin1_General_CI_AS");

            this.SendOutputGetRequest("addproduct-params", query).Wait();

            // Verify result
            Assert.Equal("test", this.ExecuteScalar($"select Name from Products where ProductId={1}"));
            Assert.Equal(100, this.ExecuteScalar($"select cost from Products where ProductId={1}"));
        }
    }
}
