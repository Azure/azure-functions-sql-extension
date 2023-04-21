// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    [Collection(IntegrationTestsCollection.Name)]
    [LogTestName]
    public class SqlOutputBindingIntegrationTests : IntegrationTestBase
    {
        public SqlOutputBindingIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }
        [Theory]
        [SqlInlineData(1, "Test", 5)]
        [SqlInlineData(0, "", 0)]
        [SqlInlineData(-500, "ABCD", 580)]
        public void AddProductTest(int id, string name, int cost, SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(AddProduct), lang);

            var query = new Dictionary<string, object>()
            {
                { "ProductId", id },
                { "Name", name },
                { "Cost", cost }
            };

            this.SendOutputPostRequest("addproduct", Utils.JsonSerializeObject(query)).Wait();

            // Verify result
            Assert.Equal(name, this.ExecuteScalar($"select Name from Products where ProductId={id}"));
            Assert.Equal(cost, this.ExecuteScalar($"select cost from Products where ProductId={id}"));
        }

        [Theory]
        [SqlInlineData(1, "Test", 5)]
        [SqlInlineData(0, "", 0)]
        [SqlInlineData(-500, "ABCD", 580)]
        // Currently Java functions return null when the parameter for name is an empty string
        // Issue link: https://github.com/Azure/azure-functions-sql-extension/issues/517
        [UnsupportedLanguages(SupportedLanguages.Java)]
        public void AddProductParamsTest(int id, string name, int cost, SupportedLanguages lang)
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
        [SqlInlineData()]
        public void AddProductArrayTest(SupportedLanguages lang)
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
                    ProductId = 1,
                    Name = "Cup",
                    Cost = 2
                },
                new Product
                {
                    ProductId = 2,
                    Name = "Glasses",
                    Cost = 12
                }
            };

            this.SendOutputPostRequest("addproducts-array", Utils.JsonSerializeObject(prods)).Wait();

            // Function call changes first 2 rows to (1, 'Cup', 2) and (2, 'Glasses', 12)
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(1) FROM Products WHERE Cost = 100"));
            Assert.Equal(2, this.ExecuteScalar("SELECT Cost FROM Products WHERE ProductId = 1"));
            Assert.Equal(2, this.ExecuteScalar("SELECT ProductId FROM Products WHERE Cost = 12"));
        }

        /// <summary>
        /// Test compatibility with converting various data types to their respective
        /// SQL server types.
        /// </summary>
        /// <param name="lang">The language to run the test against</param>
        [Theory]
        [SqlInlineData()]
        public void AddProductColumnTypesTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(AddProductColumnTypes), lang, true);

            var queryParameters = new Dictionary<string, string>()
            {
                { "productId", "999" }
            };

            this.SendOutputGetRequest("addproduct-columntypes", queryParameters).Wait();

            // If we get here then the test is successful - an exception will be thrown if there were any problems
        }

        [Theory]
        [SqlInlineData()]
        [UnsupportedLanguages(SupportedLanguages.JavaScript, SupportedLanguages.PowerShell, SupportedLanguages.Java, SupportedLanguages.OutOfProc, SupportedLanguages.Python)] // Collectors are only available in C#
        public void AddProductsCollectorTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(AddProductsCollector), lang);

            // Function should add 5000 rows to the table
            this.SendOutputGetRequest("addproducts-collector").Wait();

            Assert.Equal(5000, this.ExecuteScalar("SELECT COUNT(1) FROM Products"));
        }

        [Theory]
        [SqlInlineData()]
        public void QueueTriggerProductsTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(QueueTriggerProducts), lang);

            string uri = $"http://localhost:{this.Port}/admin/functions/QueueTriggerProducts";
            string json = /*lang=json*/ "{ 'input': 'Test Data' }";

            this.SendPostRequest(uri, json).Wait();

            Thread.Sleep(5000);

            // Function should add 100 rows
            Assert.Equal(100, this.ExecuteScalar("SELECT COUNT(1) FROM Products"));
        }

        [Theory]
        [SqlInlineData()]
        public void TimerTriggerProductsTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(TimerTriggerProducts), lang);

            // Since this function runs on a schedule (every 5 seconds), we don't need to invoke it.
            // We will wait 6 seconds to guarantee that it has been fired at least once, and check that at least 1000 rows of data has been added.
            Thread.Sleep(6000);

            int rowsAdded = (int)this.ExecuteScalar("SELECT COUNT(1) FROM Products");
            Assert.True(rowsAdded >= 1000);
        }

        [Theory]
        [SqlInlineData()]
        public void AddProductExtraColumnsTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(AddProductExtraColumns), lang, true);

            // Since ProductExtraColumns has columns that does not exist in the table,
            // no rows should be added to the table.
            Assert.Throws<AggregateException>(() => this.SendOutputGetRequest("addproduct-extracolumns").Wait());
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM Products"));
        }

        [Theory]
        [SqlInlineData()]
        public void AddProductMissingColumnsTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(AddProductMissingColumns), lang, true);

            // Even though the ProductMissingColumns object is missing the Cost column,
            // the row should still be added successfully since Cost can be null.
            this.SendOutputPostRequest("addproduct-missingcolumns", string.Empty).Wait();
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM Products"));
        }

        [Theory]
        [SqlInlineData()]
        public void AddProductMissingColumnsNotNullTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(AddProductMissingColumnsExceptionFunction), lang, true);

            // Since the Sql table does not allow null for the Cost column,
            // inserting a row without a Cost value should throw an Exception.
            Assert.Throws<AggregateException>(() => this.SendOutputPostRequest("addproduct-missingcolumnsexception", string.Empty).Wait());
        }

        [Theory]
        [SqlInlineData()]
        public void AddProductNoPartialUpsertTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(AddProductsNoPartialUpsert), lang, true);

            Assert.Throws<AggregateException>(() => this.SendOutputPostRequest("addproducts-nopartialupsert", string.Empty).Wait());
            // No rows should be upserted since there was a row with an invalid value
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsNameNotNull"));
        }

        /// <summary>
        /// Tests that for tables with an identity column we are able to insert items.
        /// </summary>
        [Theory]
        [SqlInlineData()]
        public void AddProductWithIdentity(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(AddProductWithIdentityColumn), lang);
            // Identity column (ProductId) is left out for new items
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
        [SqlInlineData()]
        public void AddProductsWithIdentityColumnArray(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(AddProductsWithIdentityColumnArray), lang);
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            this.SendOutputGetRequest(nameof(AddProductsWithIdentityColumnArray)).Wait();
            // Multiple items should have been inserted
            Assert.Equal(2, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
        }

        /// <summary>
        /// Tests that for tables with multiple primary columns (including an identity column) we are able to
        /// insert items.
        /// </summary>
        [Theory]
        [SqlInlineData()]
        public void AddProductWithIdentity_MultiplePrimaryColumns(SupportedLanguages lang)
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
        [SqlInlineData()]
        public void AddProductWithIdentity_SpecifyIdentityColumn(SupportedLanguages lang)
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
        [SqlInlineData()]
        public void AddProductWithIdentity_NoIdentityColumn(SupportedLanguages lang)
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
        [SqlInlineData()]
        public void AddProductWithIdentity_MissingPrimaryColumn(SupportedLanguages lang)
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
        /// Tests that a row is inserted successfully when the object is missing
        /// the primary key column with a default value.
        /// </summary>
        [Theory]
        [SqlInlineData()]
        public void AddProductWithDefaultPKTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(AddProductWithDefaultPK), lang);
            var product = new Dictionary<string, object>()
            {
                { "Name", "MyProduct" },
                { "Cost", 1 }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithDefaultPK"));
            this.SendOutputPostRequest("addproductwithdefaultpk", Utils.JsonSerializeObject(product)).Wait();
            this.SendOutputPostRequest("addproductwithdefaultpk", Utils.JsonSerializeObject(product)).Wait();
            Assert.Equal(2, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithDefaultPK"));
        }

        /// <summary>
        /// Tests that when using an unsupported database the expected error is thrown
        /// </summary>
        [Theory]
        [SqlInlineData()]
        [UnsupportedLanguages(SupportedLanguages.OutOfProc)]
        public async Task UnsupportedDatabaseThrows(SupportedLanguages lang)
        {
            // Change database compat level to unsupported version
            this.ExecuteNonQuery($"ALTER DATABASE {this.DatabaseName} SET COMPATIBILITY_LEVEL = 120;");

            var foundExpectedMessageSource = new TaskCompletionSource<bool>();
            this.StartFunctionHost(nameof(AddProductParams), lang, false, (object sender, DataReceivedEventArgs e) =>
            {
                if (e.Data.Contains("SQL bindings require a database compatibility level of 130 or higher to function. Current compatibility level = 120"))
                {
                    foundExpectedMessageSource.SetResult(true);
                }
            });

            var query = new Dictionary<string, string>()
            {
                { "productId", "1" },
                { "name", "test" },
                { "cost", "100" }
            };

            // The upsert should fail since the database compat level is not supported
            Exception exception = Assert.Throws<AggregateException>(() => this.SendOutputGetRequest("addproduct-params", query).Wait());
            // Verify the message contains the expected error so that other errors don't mistakenly make this test pass
            // Wait 2sec for message to get processed to account for delays reading output
            await foundExpectedMessageSource.Task.TimeoutAfter(TimeSpan.FromMilliseconds(2000), $"Timed out waiting for expected error message");
        }

        /// <summary>
        /// Tests that upserting to a case sensitive database works correctly.
        /// </summary>
        [Theory]
        [SqlInlineData()]
        public void AddProductToCaseSensitiveDatabase(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(AddProduct), lang);

            // Change database collation to case sensitive
            this.ExecuteNonQuery($"ALTER DATABASE {this.DatabaseName} SET Single_User WITH ROLLBACK IMMEDIATE; ALTER DATABASE {this.DatabaseName} COLLATE Latin1_General_CS_AS; ALTER DATABASE {this.DatabaseName} SET Multi_User;");

            var query = new Dictionary<string, object>()
            {
                { "ProductId", 0 },
                { "Name", "test" },
                { "Cost", 100 }
            };

            this.SendOutputPostRequest("addproduct", Utils.JsonSerializeObject(query)).Wait();

            // Verify result
            Assert.Equal("test", this.ExecuteScalar($"select Name from Products where ProductId=0"));
            Assert.Equal(100, this.ExecuteScalar($"select Cost from Products where ProductId=0"));

            // Change database collation back to case insensitive
            this.ExecuteNonQuery($"ALTER DATABASE {this.DatabaseName} SET Single_User WITH ROLLBACK IMMEDIATE; ALTER DATABASE {this.DatabaseName} COLLATE Latin1_General_CI_AS; ALTER DATABASE {this.DatabaseName} SET Multi_User;");
        }

        /// <summary>
        /// Tests that an error is thrown when the object field names and table column names do not match.
        /// </summary>
        [Theory]
        [SqlInlineData()]
        public void AddProductIncorrectCasing(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(AddProductIncorrectCasing), lang);

            Assert.Throws<AggregateException>(() => this.SendOutputGetRequest("addproduct-incorrectcasing").Wait());
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM Products"));
        }

        /// <summary>
        /// Tests that subsequent upserts work correctly when the object properties are different from the first upsert.
        /// </summary>
        [Theory]
        [SqlInlineData()]
        public void AddProductWithDifferentPropertiesTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(AddProduct), lang);

            var query1 = new Dictionary<string, object>()
            {
                { "ProductId", 0 },
                { "Name", "test" },
                { "Cost", 100 }
            };

            var query2 = new Dictionary<string, object>()
            {
                { "ProductId", 0 },
                { "Name", "test2" }
            };

            this.SendOutputPostRequest("addproduct", Utils.JsonSerializeObject(query1)).Wait();
            this.SendOutputPostRequest("addproduct", Utils.JsonSerializeObject(query2)).Wait();

            // Verify result
            Assert.Equal("test2", this.ExecuteScalar($"select Name from Products where ProductId=0"));
        }

        /// <summary>
        /// Tests that when upserting an item with no properties, an error is thrown.
        /// </summary>
        [Theory]
        [SqlInlineData()]
        // Only the JavaScript function passes an empty JSON to the SQL extension.
        // C#, Java, and Python throw an error while creating the Product object in the function and in PowerShell,
        // the JSON would be passed as {"ProductId": null, "Name": null, "Cost": null}.
        [UnsupportedLanguages(SupportedLanguages.CSharp, SupportedLanguages.Java, SupportedLanguages.OutOfProc, SupportedLanguages.PowerShell, SupportedLanguages.Python, SupportedLanguages.Csx)]
        public async Task NoPropertiesThrows(SupportedLanguages lang)
        {
            var foundExpectedMessageSource = new TaskCompletionSource<bool>();
            this.StartFunctionHost(nameof(AddProductParams), lang, false, (object sender, DataReceivedEventArgs e) =>
            {
                if (e.Data.Contains("No property values found in item to upsert. If using query parameters, ensure that the casing of the parameter names and the property names match."))
                {
                    foundExpectedMessageSource.SetResult(true);
                }
            });

            var query = new Dictionary<string, string>() { };

            // The upsert should fail since no parameters were passed
            Exception exception = Assert.Throws<AggregateException>(() => this.SendOutputGetRequest("addproduct-params", query).Wait());
            // Verify the message contains the expected error so that other errors don't mistakenly make this test pass
            // Wait 2sec for message to get processed to account for delays reading output
            await foundExpectedMessageSource.Task.TimeoutAfter(TimeSpan.FromMilliseconds(2000), $"Timed out waiting for expected error message");
        }

        /// <summary>
        /// Tests that an error is thrown when the upserted item contains a unsupported column type.
        /// </summary>
        [Theory]
        [SqlInlineData()]
        [UnsupportedLanguages(SupportedLanguages.OutOfProc)]
        public async Task AddProductUnsupportedTypesTest(SupportedLanguages lang)
        {
            var foundExpectedMessageSource = new TaskCompletionSource<bool>();
            this.StartFunctionHost(nameof(AddProductUnsupportedTypes), lang, true, (object sender, DataReceivedEventArgs e) =>
            {
                if (e.Data.Contains("The type(s) of the following column(s) are not supported: TextCol, NtextCol, ImageCol. See https://github.com/Azure/azure-functions-sql-extension#output-bindings for more details."))
                {
                    foundExpectedMessageSource.SetResult(true);
                }
            });

            Assert.Throws<AggregateException>(() => this.SendOutputGetRequest("addproduct-unsupportedtypes").Wait());
            await foundExpectedMessageSource.Task.TimeoutAfter(TimeSpan.FromMilliseconds(2000), $"Timed out waiting for expected error message");
        }

        /// <summary>
        /// Tests that rows are inserted correctly when the table contains default values or identity columns even if the order of
        /// the properties in the POCO/JSON object is different from the order of the columns in the table.
        /// </summary>
        [Theory]
        [SqlInlineData()]
        public void AddProductDefaultPKAndDifferentColumnOrderTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(AddProductDefaultPKAndDifferentColumnOrder), lang, true);

            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithDefaultPK"));
            this.SendOutputGetRequest("addproductdefaultpkanddifferentcolumnorder").Wait();
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithDefaultPK"));
        }
    }
}
