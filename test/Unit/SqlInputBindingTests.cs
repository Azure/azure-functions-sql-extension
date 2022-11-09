// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlConverters;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlInputBindingTests
    {
        private static readonly Mock<IConfiguration> config = new();
        private static readonly Mock<IHostIdProvider> hostIdProvider = new();
        private static readonly Mock<ILoggerFactory> loggerFactory = new();
        private static readonly Mock<ILogger> logger = new();
        private static readonly SqlConnection connection = new();

        [Fact]
        public void TestNullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() => new SqlBindingConfigProvider(null, hostIdProvider.Object, loggerFactory.Object));
            Assert.Throws<ArgumentNullException>(() => new SqlBindingConfigProvider(config.Object, null, loggerFactory.Object));
            Assert.Throws<ArgumentNullException>(() => new SqlBindingConfigProvider(config.Object, hostIdProvider.Object, null));
            Assert.Throws<ArgumentNullException>(() => new SqlConverter(null, logger.Object));
            Assert.Throws<ArgumentNullException>(() => new SqlGenericsConverter<string>(null, logger.Object));
        }

        [Fact]
        public void TestNullCommandText()
        {
            Assert.Throws<ArgumentNullException>(() => new SqlAttribute(null));
        }

        [Fact]
        public void TestNullContext()
        {
            var configProvider = new SqlBindingConfigProvider(config.Object, hostIdProvider.Object, loggerFactory.Object);
            Assert.Throws<ArgumentNullException>(() => configProvider.Initialize(null));
        }

        [Fact]
        public void TestNullBuilder()
        {
            IWebJobsBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddSql());
        }

        [Fact]
        public void TestNullCommand()
        {
            Assert.Throws<ArgumentNullException>(() => SqlBindingUtilities.ParseParameters("", null));
        }

        [Fact]
        public void TestNullArgumentsSqlAsyncEnumerableConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new SqlAsyncEnumerable<string>(connection, null));
            Assert.Throws<ArgumentNullException>(() => new SqlAsyncEnumerable<string>(null, new SqlAttribute("")));
        }

        /// <summary>
        /// SqlAsyncEnumerable should throw InvalidOperationExcepion when invoked with an invalid connection
        /// string setting and It should fail here since we're passing an empty connection string.
        /// <summary>
        [Fact]
        public void TestInvalidOperationSqlAsyncEnumerableConstructor()
        {
            Assert.Throws<InvalidOperationException>(() => new SqlAsyncEnumerable<string>(connection, new SqlAttribute("")));
        }

        [Fact]
        public void TestInvalidArgumentsBuildConnection()
        {
            var attribute = new SqlAttribute("");
            Assert.Throws<ArgumentException>(() => SqlBindingUtilities.BuildConnection(attribute.ConnectionStringSetting, config.Object));

            attribute = new SqlAttribute("")
            {
                ConnectionStringSetting = "ConnectionStringSetting"
            };
            Assert.Throws<ArgumentNullException>(() => SqlBindingUtilities.BuildConnection(attribute.ConnectionStringSetting, null));
        }

        [Fact]
        public void TestInvalidCommandType()
        {
            // Specify an invalid type
            var attribute = new SqlAttribute("")
            {
                CommandType = System.Data.CommandType.TableDirect
            };
            Assert.Throws<ArgumentException>(() => SqlBindingUtilities.BuildCommand(attribute, null));
        }

        [Fact]
        public void TestDefaultCommandType()
        {
            string query = "select * from Products";
            var attribute = new SqlAttribute(query);
            SqlCommand command = SqlBindingUtilities.BuildCommand(attribute, null);
            // CommandType should default to Text
            Assert.Equal(System.Data.CommandType.Text, command.CommandType);
            Assert.Equal(query, command.CommandText);

        }

        [Fact]
        public void TestValidCommandType()
        {
            string query = "select * from Products";
            var attribute = new SqlAttribute(query)
            {
                CommandType = System.Data.CommandType.Text
            };
            SqlCommand command = SqlBindingUtilities.BuildCommand(attribute, null);
            Assert.Equal(System.Data.CommandType.Text, command.CommandType);
            Assert.Equal(query, command.CommandText);

            string procedure = "StoredProcedure";
            attribute = new SqlAttribute(procedure)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command = SqlBindingUtilities.BuildCommand(attribute, null);
            Assert.Equal(System.Data.CommandType.StoredProcedure, command.CommandType);
            Assert.Equal(procedure, command.CommandText);
        }

        [Fact]
        public void TestMalformedParametersString()
        {
            var command = new SqlCommand();
            // Second param name doesn't start with "@"
            string parameters = "@param1=param1,param2=param2";
            Assert.Throws<ArgumentException>(() => SqlBindingUtilities.ParseParameters(parameters, command));

            // Second param not separated by "=", or contains extra "="
            parameters = "@param1=param1,@param2==param2";
            Assert.Throws<ArgumentException>(() => SqlBindingUtilities.ParseParameters(parameters, command));
            parameters = "@param1=param1,@param2;param2";
            Assert.Throws<ArgumentException>(() => SqlBindingUtilities.ParseParameters(parameters, command));
            parameters = "@param1=param1,@param2=param2=";
            Assert.Throws<ArgumentException>(() => SqlBindingUtilities.ParseParameters(parameters, command));

            // Params list not separated by "," correctly
            parameters = "@param1=param1;@param2=param2";
            Assert.Throws<ArgumentException>(() => SqlBindingUtilities.ParseParameters(parameters, command));
            parameters = "@param1=param1,@par,am2=param2";
            Assert.Throws<ArgumentException>(() => SqlBindingUtilities.ParseParameters(parameters, command));
        }

        [Fact]
        public void TestWellformedParametersString()
        {
            var command = new SqlCommand();
            string parameters = "@param1=param1,@param2=param2";
            SqlBindingUtilities.ParseParameters(parameters, command);

            // Apparently SqlParameter doesn't implement an Equals method, so have to do this manually
            Assert.Equal(2, command.Parameters.Count);
            foreach (SqlParameter param in command.Parameters)
            {
                Assert.True(param.ParameterName.Equals("@param1") || param.ParameterName.Equals("@param2"));
                if (param.ParameterName.Equals("@param1"))
                {
                    Assert.True(param.Value.Equals("param1"));
                }
                else
                {
                    Assert.True(param.Value.Equals("param2"));
                }
            }

            // Confirm we throw away empty entries at the beginning/end and ignore multiple commas in between
            // parameter pairs
            command = new SqlCommand();
            parameters = ",,@param1=param1,,@param2=param2,,,";
            SqlBindingUtilities.ParseParameters(parameters, command);

            Assert.Equal(2, command.Parameters.Count);
            foreach (SqlParameter param in command.Parameters)
            {
                Assert.True(param.ParameterName.Equals("@param1") || param.ParameterName.Equals("@param2"));
                if (param.ParameterName.Equals("@param1"))
                {
                    Assert.True(param.Value.Equals("param1"));
                }
                else
                {
                    Assert.True(param.Value.Equals("param2"));
                }
            }

            // Confirm we interpret "null" as being a NULL parameter value, and that we interpret
            // a string like "@param1=,@param2=param2" as @param1 having an empty string as its value
            command = new SqlCommand();
            parameters = "@param1=,@param2=null";
            SqlBindingUtilities.ParseParameters(parameters, command);

            Assert.Equal(2, command.Parameters.Count);
            foreach (SqlParameter param in command.Parameters)
            {
                Assert.True(param.ParameterName.Equals("@param1") || param.ParameterName.Equals("@param2"));
                if (param.ParameterName.Equals("@param1"))
                {
                    Assert.True(param.Value.Equals(string.Empty));
                }
                else
                {
                    Assert.True(param.Value.Equals(DBNull.Value));
                }
            }

            // Confirm nothing is done when parameters are not specified
            command = new SqlCommand();
            parameters = null;
            SqlBindingUtilities.ParseParameters(parameters, command);
            Assert.Equal(0, command.Parameters.Count);
        }

        [Fact]
        public async void TestWellformedDeserialization()
        {
            var arg = new SqlAttribute(string.Empty);
            var converter = new Mock<SqlGenericsConverter<TestData>>(config.Object, logger.Object);
            string json = "[{ \"ID\":1,\"Name\":\"Broom\",\"Cost\":32.5,\"Timestamp\":\"2019-11-22T06:32:15\"},{ \"ID\":2,\"Name\":\"Brush\",\"Cost\":12.3," +
                "\"Timestamp\":\"2017-01-27T03:13:11\"},{ \"ID\":3,\"Name\":\"Comb\",\"Cost\":100.12,\"Timestamp\":\"1997-05-03T10:11:56\"}]";
            converter.Setup(_ => _.BuildItemFromAttributeAsync(arg, ConvertType.IEnumerable)).ReturnsAsync(json);
            var list = new List<TestData>();
            var data1 = new TestData
            {
                ID = 1,
                Name = "Broom",
                Cost = 32.5,
                Timestamp = new DateTime(2019, 11, 22, 6, 32, 15)
            };
            var data2 = new TestData
            {
                ID = 2,
                Name = "Brush",
                Cost = 12.3,
                Timestamp = new DateTime(2017, 1, 27, 3, 13, 11)
            };
            var data3 = new TestData
            {
                ID = 3,
                Name = "Comb",
                Cost = 100.12,
                Timestamp = new DateTime(1997, 5, 3, 10, 11, 56)
            };
            list.Add(data1);
            list.Add(data2);
            list.Add(data3);
            IEnumerable<TestData> enActual = await converter.Object.ConvertAsync(arg, new CancellationToken());
            Assert.True(enActual.ToList().SequenceEqual(list));
        }

        [Fact]
        public async void TestMalformedDeserialization()
        {
            var arg = new SqlAttribute(string.Empty);
            var converter = new Mock<SqlGenericsConverter<TestData>>(config.Object, logger.Object);

            // SQL data is missing a field
            string json = /*lang=json,strict*/ "[{ \"ID\":1,\"Name\":\"Broom\",\"Timestamp\":\"2019-11-22T06:32:15\"}]";
            converter.Setup(_ => _.BuildItemFromAttributeAsync(arg, ConvertType.IEnumerable)).ReturnsAsync(json);
            var list = new List<TestData>();
            var data = new TestData
            {
                ID = 1,
                Name = "Broom",
                Cost = 0,
                Timestamp = new DateTime(2019, 11, 22, 6, 32, 15)
            };
            list.Add(data);
            IEnumerable<TestData> enActual = await converter.Object.ConvertAsync(arg, new CancellationToken());
            Assert.True(enActual.ToList().SequenceEqual(list));

            // SQL data's columns are named differently than the POCO's fields
            json = /*lang=json,strict*/ "[{ \"ID\":1,\"Product Name\":\"Broom\",\"Price\":32.5,\"Timessstamp\":\"2019-11-22T06:32:15\"}]";
            converter.Setup(_ => _.BuildItemFromAttributeAsync(arg, ConvertType.IEnumerable)).ReturnsAsync(json);
            list = new List<TestData>();
            data = new TestData
            {
                ID = 1,
                Name = null,
                Cost = 0,
            };
            list.Add(data);
            enActual = await converter.Object.ConvertAsync(arg, new CancellationToken());
            Assert.True(enActual.ToList().SequenceEqual(list));

            // Confirm that the JSON fields are case-insensitive (technically malformed string, but still works)
            json = /*lang=json,strict*/ "[{ \"id\":1,\"nAme\":\"Broom\",\"coSt\":32.5,\"TimEStamp\":\"2019-11-22T06:32:15\"}]";
            converter.Setup(_ => _.BuildItemFromAttributeAsync(arg, ConvertType.IEnumerable)).ReturnsAsync(json);
            list = new List<TestData>();
            data = new TestData
            {
                ID = 1,
                Name = "Broom",
                Cost = 32.5,
                Timestamp = new DateTime(2019, 11, 22, 6, 32, 15)
            };
            list.Add(data);
            enActual = await converter.Object.ConvertAsync(arg, new CancellationToken());
            Assert.True(enActual.ToList().SequenceEqual(list));
        }
    }
}
