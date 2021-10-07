// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Sql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace SqlExtension.Tests
{
    public class SqlOutputBindingTests
    {
        private static readonly Mock<IConfiguration> config = new Mock<IConfiguration>();

        [Fact]
        public void TestNullCollectorConstructorArguments()
        {
            var arg = new SqlAttribute(string.Empty);
            Assert.Throws<ArgumentNullException>(() => new SqlAsyncCollector<string>(config.Object, null, NullLoggerFactory.Instance));
            Assert.Throws<ArgumentNullException>(() => new SqlAsyncCollector<string>(null, arg, NullLoggerFactory.Instance));
        }

        [Fact]
        public async Task TestAddAsync()
        {
            // Really a pretty silly test. Just confirms that the SQL connection is only opened when FlushAsync is called,
            // because otherwise we would get an exception in AddAsync (since the SQL connection in the wrapper is null)
            var arg = new SqlAttribute(string.Empty);
            var collector = new SqlAsyncCollector<TestData>(config.Object, arg, NullLoggerFactory.Instance);
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
        [InlineData("dbo.Products", "'dbo'", "'Products'")]
        [InlineData("Products", "SCHEMA_NAME()", "'Products'")]
        [InlineData("[dbo].[Products]", "'dbo'", "'Products'")]
        [InlineData("[dbo].Products", "'dbo'", "'Products'")]
        [InlineData("dbo.[Products]", "'dbo'", "'Products'")]
        [InlineData("[Prod'ucts]", "SCHEMA_NAME()", "'Prod''ucts'")]
        public void TestGetTableAndSchema(string fullName, string expectedSchema, string expectedTableName)
        {
            SqlBindingUtilities.GetTableAndSchema(fullName, out string quotedSchema, out string quotedTableName);
            Assert.Equal(expectedSchema, quotedSchema);
            Assert.Equal(expectedTableName, quotedTableName);
        }
    }
}
