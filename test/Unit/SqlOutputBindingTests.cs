// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Moq;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Xunit;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlOutputBindingTests
    {
        private static readonly Mock<IConfiguration> config = new();
        private static readonly Mock<ILogger> logger = new();

        [Fact]
        public void TestNullCollectorConstructorArguments()
        {
            var arg = new SqlAttribute(string.Empty);
            Assert.Throws<ArgumentNullException>(() => new SqlAsyncCollector<string>(config.Object, null, logger.Object));
            Assert.Throws<ArgumentNullException>(() => new SqlAsyncCollector<string>(null, arg, logger.Object));
        }

        [Fact]
        public async Task TestAddAsync()
        {
            // Really a pretty silly test. Just confirms that the SQL connection is only opened when FlushAsync is called,
            // because otherwise we would get an exception in AddAsync (since the SQL connection in the wrapper is null)
            var arg = new SqlAttribute(string.Empty);
            var collector = new SqlAsyncCollector<TestData>(config.Object, arg, logger.Object);
            var data = new TestData
            {
                ID = 1,
                Name = "Data",
                Cost = 10,
                Timestamp = new DateTime(2019, 11, 22, 6, 32, 15)
            };
            await collector.AddAsync(data);
        }

        [Theory]
        [InlineData("dbo.Products", "dbo", "'dbo'", "Products", "'Products'", "dbo.Products", "'[dbo].[Products]'", "[dbo].[Products]")] // Simple full name
        [InlineData("Products", "SCHEMA_NAME()", "SCHEMA_NAME()", "Products", "'Products'", "Products", "'[Products]'", "[Products]")] // Simple no schema
        [InlineData("[dbo].[Products]", "dbo", "'dbo'", "Products", "'Products'", "dbo.Products", "'[dbo].[Products]'", "[dbo].[Products]")] // Simple full name bracket quoted
        [InlineData("[dbo].Products", "dbo", "'dbo'", "Products", "'Products'", "dbo.Products", "'[dbo].[Products]'", "[dbo].[Products]")] // Simple full name only schema bracket quoted
        [InlineData("dbo.[Products]", "dbo", "'dbo'", "Products", "'Products'", "dbo.Products", "'[dbo].[Products]'", "[dbo].[Products]")] // Simple full name only name bracket quoted
        [InlineData("[My'Schema].[Prod'ucts]", "My'Schema", "'My''Schema'", "Prod'ucts", "'Prod''ucts'", "My'Schema.Prod'ucts", "'[My''Schema].[Prod''ucts]'", "[My'Schema].[Prod'ucts]")] // Full name with single quotes in schema and name
        [InlineData("[My]]Schema].[My]]Object]", "My]Schema", "'My]Schema'", "My]Object", "'My]Object'", "My]Schema.My]Object", "'[My]]Schema].[My]]Object]'", "[My]]Schema].[My]]Object]")] // Full name with brackets in schema and name
        public void TestSqlObject(string fullName,
            string expectedSchema,
            string expectedQuotedSchema,
            string expectedTableName,
            string expectedSchemaTableName,
            string expectedFullName,
            string expectedQuotedFullName,
            string expectedBracketQuotedFullName)
        {
            var sqlObj = new SqlObject(fullName);
            Assert.Equal(expectedSchema, sqlObj.Schema);
            Assert.Equal(expectedQuotedSchema, sqlObj.QuotedSchema);
            Assert.Equal(expectedTableName, sqlObj.Name);
            Assert.Equal(expectedSchemaTableName, sqlObj.QuotedName);
            Assert.Equal(expectedFullName, sqlObj.FullName);
            Assert.Equal(expectedQuotedFullName, sqlObj.QuotedFullName);
            Assert.Equal(expectedBracketQuotedFullName, sqlObj.BracketQuotedFullName);
        }

        [Theory]
        [InlineData("myschema.my'table", "Expected but did not find a closing quotation mark after the character string 'table.\n")]
        [InlineData("my'schema.mytable", "Expected but did not find a closing quotation mark after the character string 'schema.mytable.\n")]
        [InlineData("schema.mytable", "Incorrect syntax near schema.\n")] // 'schema' is a keyword and needs to be bracket-quoted to be used as the schema name.
        [InlineData("myschema.table", "Incorrect syntax near ..\n")] // 'table' is a keyword and needs to be bracket-quoted to be used as the table name.
        public void TestSqlObjectParseError(string fullName, string expectedError)
        {
            string expectedErrorMessage = "Encountered error(s) while parsing schema and object name:\n" + expectedError;
            string errorMessage = Assert.Throws<InvalidOperationException>(() => new SqlObject(fullName)).Message;
            Assert.Equal(expectedErrorMessage, errorMessage);
        }

        [Theory]
        [InlineData("columnName", "[columnName]")]
        [InlineData("column]Name", "[column]]Name]")]
        [InlineData("col[umn]Name", "[col[umn]]Name]")]
        public void TestAsBracketQuotedString(string s, string expectedResult)
        {
            string result = s.AsBracketQuotedString();
            Assert.Equal(expectedResult, result);
        }
    }
}
