// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.OutputBindingSamples;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Xunit;
using Xunit.Abstractions;
using Newtonsoft.Json;

namespace Microsoft.Azure.Functions.Worker.Sql.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class SqlOutputBindingIntegrationTests : IntegrationTestBase
    {
        public SqlOutputBindingIntegrationTests(ITestOutputHelper output) : base(output)
        {
        }
        [Theory]
        [InlineData(1, "Test", 5)]
        [InlineData(0, "", 0)]
        [InlineData(-500, "ABCD", 580)]
        public void AddProductTest(int id, string name, int cost)
        {
            this.StartFunctionHost(nameof(AddProduct));

            var query = new Dictionary<string, object>()
            {
                { "productId", id },
                { "name", name },
                { "cost", cost }
            };

            this.SendOutputPostRequest("addproduct", JsonConvert.SerializeObject(query)).Wait();

            // Verify result
            Assert.Equal(name, this.ExecuteScalar($"select Name from Products where ProductId={id}"));
            Assert.Equal(cost, this.ExecuteScalar($"select cost from Products where ProductId={id}"));
        }

        [Theory]
        [InlineData(1, "Test", 5)]
        [InlineData(0, "", 0)]
        [InlineData(-500, "ABCD", 580)]
        public void AddProductParamsTest(int id, string name, int cost)
        {
            this.StartFunctionHost(nameof(AddProductParams));

            var query = new Dictionary<string, string>()
            {
                { "productId", id.ToString() },
                { "name", name },
                { "cost", cost.ToString() }
            };

            this.SendOutputPostRequest("addproduct-params", query).Wait();

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

        /*         /// <summary>
                /// Test compatability with converting various data types to their respective
                /// SQL server types.
                /// </summary>
                [Theory]
                [InlineData()]
                public void AddProductColumnTypesTest()
                {
                    this.StartFunctionHost(nameof(AddProductColumnTypes), true);

                    var queryParameters = new Dictionary<string, string>()
                    {
                        { "productId", "999" }
                    };

                    this.SendOutputGetRequest("addproduct-columntypes", queryParameters).Wait();

                    // If we get here then the test is successful - an exception will be thrown if there were any problems
                }

                [Theory]
                [InlineData()]
                public void AddProductExtraColumnsTest()
                {
                    this.StartFunctionHost(nameof(AddProductExtraColumns), true);

                    // Since ProductExtraColumns has columns that does not exist in the table,
                    // no rows should be added to the table.
                    Assert.Throws<AggregateException>(() => this.SendOutputGetRequest("addproduct-extracolumns").Wait());
                    Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM Products"));
                }

                [Theory]
                [InlineData()]
                public void AddProductMissingColumnsTest()
                {
                    this.StartFunctionHost(nameof(AddProductMissingColumns), true);

                    // Even though the ProductMissingColumns object is missing the Cost column,
                    // the row should still be added successfully since Cost can be null.
                    this.SendOutputPostRequest("addproduct-missingcolumns", string.Empty).Wait();
                    Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM Products"));
                }

                [Theory]
                [InlineData()]
                public void AddProductMissingColumnsNotNullTest()
                {
                    this.StartFunctionHost(nameof(AddProductMissingColumnsExceptionFunction), true);

                    // Since the Sql table does not allow null for the Cost column,
                    // inserting a row without a Cost value should throw an Exception.
                    Assert.Throws<AggregateException>(() => this.SendOutputPostRequest("addproduct-missingcolumnsexception", string.Empty).Wait());
                }

                /* [Theory]
                [InlineData()]
                public void AddProductNoPartialUpsertTest()
                {
                    this.StartFunctionHost(nameof(AddProductsNoPartialUpsert), true);

                    Assert.Throws<AggregateException>(() => this.SendOutputPostRequest("addproducts-nopartialupsert", string.Empty).Wait());
                    // No rows should be upserted since there was a row with an invalid value
                    Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsNameNotNull"));
                } */

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
            this.SendOutputGetRequest(nameof(AddProductWithIdentityColumn), query).Wait();
            // Product should have been inserted correctly even without an ID when there's an identity column present
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
        }

        /// <summary>
        /// Tests that for tables with an identity column we are able to insert multiple items at once
        /// </summary>
        [Fact]
        public void AddProductsWithIdentityColumnArray()
        {
            this.StartFunctionHost(nameof(AddProductsWithIdentityColumnArray));
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
            this.SendOutputGetRequest(nameof(AddProductsWithIdentityColumnArray)).Wait();
            // Multiple items should have been inserted
            Assert.Equal(2, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithIdentity"));
        }

        /// <summary>
        /// Tests that for tables with multiple primary columns (including an identity column) we are able to
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
            this.SendOutputGetRequest(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity), query).Wait();
            // Product should have been inserted correctly even without an ID when there's an identity column present
            Assert.Equal(1, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithMultiplePrimaryColumnsAndIdentity"));
        }

        /// <summary>
        /// Tests that when using a table with an identity column that if the identity column is specified
        /// by the function we handle inserting/updating that correctly.
        /// </summary>
        [Fact]
        public void AddProductWithIdentity_SpecifyIdentityColumn()
        {
            this.StartFunctionHost(nameof(AddProductWithIdentityColumnIncluded));
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
        [Fact]
        public void AddProductWithIdentity_NoIdentityColumn()
        {
            this.StartFunctionHost(nameof(AddProductWithIdentityColumnIncluded));
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
            Assert.Throws<AggregateException>(() => this.SendOutputGetRequest(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity), query).Wait());
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
            // Set table info cache timeout to 0 minutes so that new collation gets picked up
            var environmentVariables = new Dictionary<string, string>()
            {
                { "AZ_FUNC_TABLE_INFO_CACHE_TIMEOUT_MINUTES", "0" }
            };
            this.StartFunctionHost(nameof(AddProductParams), false, null, environmentVariables);

            // Change database collation to case sensitive
            this.ExecuteNonQuery($"ALTER DATABASE {this.DatabaseName} SET Single_User WITH ROLLBACK IMMEDIATE; ALTER DATABASE {this.DatabaseName} COLLATE Latin1_General_CS_AS; ALTER DATABASE {this.DatabaseName} SET Multi_User;");

            var query = new Dictionary<string, string>()
            {
                { "productId", "1" },
                { "name", "test" },
                { "cost", "100" }
            };

            // The upsert should fail since the database is case sensitive and the column name "ProductId"
            // does not match the POCO field "ProductID"
            Assert.Throws<AggregateException>(() => this.SendOutputPostRequest("addproduct-params", query).Wait());

            // Change database collation back to case insensitive
            this.ExecuteNonQuery($"ALTER DATABASE {this.DatabaseName} SET Single_User WITH ROLLBACK IMMEDIATE; ALTER DATABASE {this.DatabaseName} COLLATE Latin1_General_CI_AS; ALTER DATABASE {this.DatabaseName} SET Multi_User;");

            this.SendOutputPostRequest("addproduct-params", query).Wait();

            // Verify result
            Assert.Equal("test", this.ExecuteScalar($"select Name from Products where ProductId={1}"));
            Assert.Equal(100, this.ExecuteScalar($"select cost from Products where ProductId={1}"));
        }

        /// <summary>
        /// Tests that a row is inserted successfully when the object is missing
        /// the primary key column with a default value.
        /// </summary>
        [Fact]
        public void AddProductWithDefaultPKTest()
        {
            this.StartFunctionHost(nameof(AddProductWithDefaultPK));
            var product = new Dictionary<string, object>()
            {
                { "name", "MyProduct" },
                { "cost", 1 }
            };
            Assert.Equal(0, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithDefaultPK"));
            this.SendOutputPostRequest("addproductwithdefaultpk", JsonConvert.SerializeObject(product)).Wait();
            this.SendOutputPostRequest("addproductwithdefaultpk", JsonConvert.SerializeObject(product)).Wait();
            Assert.Equal(2, this.ExecuteScalar("SELECT COUNT(*) FROM dbo.ProductsWithDefaultPK"));
        }
    }
}
