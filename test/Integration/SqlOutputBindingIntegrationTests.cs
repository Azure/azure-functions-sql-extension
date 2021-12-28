// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples;
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

        private async Task<HttpResponseMessage> SendOutputRequest(string functionName, IDictionary<string, string> query = null)
        {
            string requestUri = $"http://localhost:{this.Port}/api/{functionName}";

            if (query != null)
            {
                requestUri = QueryHelpers.AddQueryString(requestUri, query);
            }

            return await this.SendGetRequest(requestUri);
        }

        [Theory]
        [InlineData(1, "Test", 5)]
        [InlineData(0, "", 0)]
        [InlineData(-500, "ABCD", 580)]
        public void AddProductTest(int id, string name, int cost)
        {
            this.StartFunctionHost(nameof(AddProduct));

            var query = new Dictionary<string, string>()
            {
                { "id", id.ToString() },
                { "name", name },
                { "cost", cost.ToString() }
            };

            this.SendOutputRequest(nameof(AddProduct), query).Wait();

            // Verify result
            Assert.Equal(name, this.ExecuteScalar($"select Name from Products where ProductId={id}"));
            Assert.Equal(cost, this.ExecuteScalar($"select cost from Products where ProductId={id}"));
        }

        [Fact]
        public void AddProductArrayTest()
        {
            this.StartFunctionHost(nameof(AddProductsArray));

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

        [Fact]
        public void AddProductsCollectorTest()
        {
            this.StartFunctionHost(nameof(AddProductsCollector));

            // Function should add 5000 rows to the table
            this.SendOutputRequest("addproducts-collector").Wait();

            Assert.Equal(5000, this.ExecuteScalar("SELECT COUNT(1) FROM Products"));
        }

        [Fact]
        public void QueueTriggerProductsTest()
        {
            this.StartFunctionHost(nameof(QueueTriggerProducts));

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
            // Timer trigger function requires local storage to be running
            this.StartAzurite();
            this.StartFunctionHost(nameof(TimerTriggerProducts));

            // Since this function runs on a schedule (every 5 seconds), we don't need to invoke it.
            // We will wait 6 seconds to guarantee that it has been fired at least once, and check that at least 1000 rows of data has been added.
            Thread.Sleep(6000);

            int rowsAdded = (int)this.ExecuteScalar("SELECT COUNT(1) FROM Products");
            Assert.True(rowsAdded >= 1000);
        }

        [Fact]
        public void AddProductExtraColumnsTest()
        {
            this.StartFunctionHost(nameof(AddProductExtraColumns), true);

            // Since ProductExtraColumns has columns that does not exist in the table,
            // no rows should be added to the table.
            Assert.Throws<AggregateException>(() => this.SendOutputRequest("addproduct-extracolumns").Wait());
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM Products"));
        }

        [Fact]
        public void AddProductMissingColumnsTest()
        {
            this.StartFunctionHost(nameof(AddProductMissingColumns), true);

            // Even though the ProductMissingColumns object is missing the Cost column,
            // the row should still be added successfully since Cost can be null.
            this.SendOutputRequest("addproduct-missingcolumns").Wait();
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM Products"));
        }

        [Fact]
        public void AddProductMissingColumnsNotNullTest()
        {
            this.StartFunctionHost(nameof(AddProductMissingColumnsExceptionFunction), true);

            // Since the Sql table does not allow null for the Cost column,
            // inserting a row without a Cost value should throw an Exception.
            Assert.Throws<AggregateException>(() => this.SendOutputRequest("addproduct-missingcolumnsexception").Wait());
        }

        [Fact]
        public void AddProductNoPartialUpsertTest()
        {
            this.StartFunctionHost(nameof(AddProductsNoPartialUpsert), true);

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
            this.StartFunctionHost(nameof(AddProductWithIdentityColumn));
            // Identity column (ProductID) is left out for new items
            var query = new Dictionary<string, string>()
            {
                { "name", "MyProduct" },
                { "cost", "1" }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            this.SendOutputRequest(nameof(AddProductWithIdentityColumn), query).Wait();
            // Product should have been inserted correctly even without an ID when there's an identity column present
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
        }

        // TODO Add functionality to update existing rows in tables with identity column

        /// <summary>
        /// Tests that for tables with multiple primary columns (including an itemtity column) we are able to
        /// insert items.
        /// </summary>
        [Fact]
        public void AddProductWithIdentity_MultiplePrimaryColumns()
        {
            this.StartFunctionHost(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity));
            var query = new Dictionary<string, string>()
            {
                { "externalId", "101" },
                { "name", "MyProduct" },
                { "cost", "1" }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
            this.SendOutputRequest(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity), query).Wait();
            // Product should have been inserted correctly even without an ID when there's an identity column present
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
        }

        /// <summary>
        /// Tests that when using a table with an identity column that if the identity column is specified
        /// by the function an error is thrown.
        /// </summary>
        [Fact]
        public void AddProductWithIdentity_SpecifyIdentityColumn()
        {
            this.StartFunctionHost(nameof(AddProductIncludeIdentity), true);
            // Identity column (ProductID) should be left out for new items
            var query = new Dictionary<string, string>()
            {
                { "productId", "1" },
                { "name", "MyProduct" },
                { "cost", "1" }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            Assert.Throws<AggregateException>(() => this.SendOutputRequest(nameof(AddProductIncludeIdentity), query).Wait());
            // Nothing should have been inserted
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
        }

        /// <summary>
        /// Tests that when using a table with an identity column along with other primary 
        /// keys an error is thrown if at least one of the primary keys is missing.
        /// </summary>
        [Fact]
        public void AddProductWithIdentity_MissingPrimaryColumn()
        {
            this.StartFunctionHost(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity));
            var query = new Dictionary<string, string>()
            {
                // Missing externalId
                { "name", "MyProduct" },
                { "cost", "1" }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
            Assert.Throws<AggregateException>(() => this.SendOutputRequest(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity), query).Wait());
            // Nothing should have been inserted
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
        }
    }
}
