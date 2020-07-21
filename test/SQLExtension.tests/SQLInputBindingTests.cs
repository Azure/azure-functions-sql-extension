// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Moq;
using System;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.Sql;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlConverters;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Azure.WebJobs;

namespace SqlExtension.Tests
{
    public class SqlInputBindingTests
    {
        private static Mock<IConfiguration> config = new Mock<IConfiguration>();
        private static SqlConnection connection = new SqlConnection();

        [Fact]
        public void TestNullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() => new SqlBindingConfigProvider(null));
            IConfiguration config = null;
            Assert.Throws<ArgumentNullException>(() => new SqlConverter(config));
            Assert.Throws<ArgumentNullException>(() => new SqlGenericsConverter<string>(config));
        }

        [Fact]
        public void TestNullCommandText()
        {
            Assert.Throws<ArgumentNullException>(() => new SqlAttribute(null));
        }

        [Fact]
        public void TestNullContext()
        {
            var configProvider = new SqlBindingConfigProvider(config.Object);
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
            try
            {
                SqlBindingUtilities.ParseParameters("", null);
            }
            catch (ArgumentNullException e)
            {
                Assert.Equal("Value cannot be null. (Parameter 'command')", e.Message);
            }
        }

        [Fact]
        public void TestNullArgumentsSqlAsyncEnumerableConstructor()
        {
            try
            {
                new SqlAsyncEnumerable<string>(connection, null);
            }
            catch (ArgumentNullException e)
            {
                Assert.Equal("Value cannot be null. (Parameter 'attribute')", e.Message);
            }

            try
            {
                new SqlAsyncEnumerable<string>(null, new SqlAttribute(""));
            }
            catch (ArgumentNullException e)
            {
                Assert.Equal("Value cannot be null. (Parameter 'connection')", e.Message);
            }
        }

        [Fact]
        public void TestInvalidOperationEnumerator()
        {
            var enumerable = new SqlAsyncEnumerable<string>(connection, new SqlAttribute(""));
            var enumerator = enumerable.GetAsyncEnumerator();
            try
            {
                var current = enumerator.Current;
            }
            catch (InvalidOperationException e)
            {
                Assert.Equal("Invalid attempt to get current element when no data is present", e.Message);
            }
        }

        [Fact]
        public void TestInvalidArgumentsBuildConnection()
        {
            try
            {
                var attribute = new SqlAttribute("");
                SqlBindingUtilities.BuildConnection(attribute, config.Object);
            }
            catch (InvalidOperationException e)
            {
                Assert.Equal("Must specify a ConnectionStringSetting, which refers to the name of an app setting which contains" +
                        "the SQL connection string, to connect to your SQL server instance.", e.Message);
            }

            try
            {
                var attribute = new SqlAttribute("");
                attribute.ConnectionStringSetting = "ConnectionStringSetting";
                SqlBindingUtilities.BuildConnection(attribute, null);
            }
            catch (ArgumentNullException e)
            {
                Assert.Equal("Value cannot be null. (Parameter 'configuration')", e.Message);
            }
        }

        [Fact]
        public void TestInvalidCommandType()
        {
            // Specify an invalid type
            try
            {
                var attribute = new SqlAttribute("");
                attribute.CommandType = System.Data.CommandType.TableDirect;
                SqlBindingUtilities.BuildCommand(attribute, null);
            }
            catch (ArgumentException e)
            {
                Assert.Equal("The Type of the SQL attribute for an input binding must be either CommandType.Text for a plain text" +
                    "SQL query, or CommandType.StoredProcedure for a stored procedure.", e.Message);
            }

            // Don't specify a type at all
            try
            {
                var attribute = new SqlAttribute("");
                SqlBindingUtilities.BuildCommand(attribute, null);
            }
            catch (ArgumentException e)
            {
                Assert.Equal("The Type of the SQL attribute for an input binding must be either CommandType.Text for a plain text" +
                    "SQL query, or CommandType.StoredProcedure for a stored procedure.", e.Message);
            }
        }

        [Fact]
        public void TestValidCommandType()
        {
            var query = "select * from Products";
            var attribute = new SqlAttribute(query);
            attribute.CommandType = System.Data.CommandType.Text;
            var command = SqlBindingUtilities.BuildCommand(attribute, null);
            Assert.Equal(System.Data.CommandType.Text, command.CommandType);
            Assert.Equal(query, command.CommandText);

            var procedure = "StoredProceudre";
            attribute = new SqlAttribute(procedure);
            attribute.CommandType = System.Data.CommandType.StoredProcedure;
            command = SqlBindingUtilities.BuildCommand(attribute, null);
            Assert.Equal(System.Data.CommandType.StoredProcedure, command.CommandType);
            Assert.Equal(procedure, command.CommandText);
        }

        [Fact]
        public void TestMalformedParametersString()
        {
            var command = new SqlCommand();
            // Second param name doesn't start with "@"
            try
            {
                string parameters = "@param1=param1,param2=param2";
                SqlBindingUtilities.ParseParameters(parameters, command);
            }
            catch (ArgumentException e)
            {
                Assert.Equal("Parameter name must start with \"@\", i.e. \"@param1=param1,@param2=param2\"", e.Message);
            }

            // Second param not separated by "=", or contains extra "="
            try
            {
                string parameters = "@param1=param1,@param2==param2";
                SqlBindingUtilities.ParseParameters(parameters, command);
            }
            catch (ArgumentException e)
            {
                Assert.Equal("Parameters must be separated by \",\" and parameter name and parameter value must be separated by \"=\", " +
                        "i.e. \"@param1=param1,@param2=param2\"", e.Message);
            }
            try
            {
                string parameters = "@param1=param1,@param2;param2";
                SqlBindingUtilities.ParseParameters(parameters, command);
            }
            catch (ArgumentException e)
            {
                Assert.Equal("Parameters must be separated by \",\" and parameter name and parameter value must be separated by \"=\", " +
                        "i.e. \"@param1=param1,@param2=param2\"", e.Message);
            }
            try
            {
                string parameters = "@param1=param1,@param2=param2=";
                SqlBindingUtilities.ParseParameters(parameters, command);
            }
            catch (ArgumentException e)
            {
                Assert.Equal("Parameters must be separated by \",\" and parameter name and parameter value must be separated by \"=\", " +
                        "i.e. \"@param1=param1,@param2=param2\"", e.Message);
            }

            // Params list not separated by "," correctly
            try
            {
                string parameters = "@param1=param1;@param2=param2";
                SqlBindingUtilities.ParseParameters(parameters, command);
            }
            catch (ArgumentException e)
            {
                Assert.Equal("Parameters must be separated by \",\" and parameter name and parameter value must be separated by \"=\", " +
                        "i.e. \"@param1=param1,@param2=param2\"", e.Message);
            }
            try
            {
                string parameters = "@param1=param1,,@param2=param2";
                SqlBindingUtilities.ParseParameters(parameters, command);
            }
            catch (ArgumentException e)
            {
                Assert.Equal("Parameters must be separated by \",\" and parameter name and parameter value must be separated by \"=\", " +
                        "i.e. \"@param1=param1,@param2=param2\"", e.Message);
            }
            try
            {
                string parameters = "@param1=param1,@par,am2=param2";
                SqlBindingUtilities.ParseParameters(parameters, command);
            }
            catch (ArgumentException e)
            {
                Assert.Equal("Parameters must be separated by \",\" and parameter name and parameter value must be separated by \"=\", " +
                        "i.e. \"@param1=param1,@param2=param2\"", e.Message);
            }
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

            // Confirm we throw away empty entries at the beginning/end
            command = new SqlCommand();
            parameters = ",,@param1=param1,@param2=param2,,,";
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

            // Confirm nothing is done when parameters are not specified
            command = new SqlCommand();
            parameters = null;
            SqlBindingUtilities.ParseParameters(parameters, command);
            Assert.Equal(0, command.Parameters.Count);
        }

        [Fact]
        public void TestWellformedDeserialization()
        {
            var arg = new SqlAttribute(string.Empty);
            var converter = new Mock<SqlGenericsConverter<TestData>>(config.Object);
            string json = "[{ \"ID\":1,\"Name\":\"Broom\",\"Cost\":32.5,\"Timestamp\":\"2019-11-22T06:32:15\"},{ \"ID\":2,\"Name\":\"Brush\",\"Cost\":12.3," +
                "\"Timestamp\":\"2017-01-27T03:13:11\"},{ \"ID\":3,\"Name\":\"Comb\",\"Cost\":100.12,\"Timestamp\":\"1997-05-03T10:11:56\"}]";
            converter.Setup(_ => _.BuildItemFromAttribute(arg)).ReturnsAsync(json);
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
            IEnumerable<TestData> enActual = converter.Object.Convert(arg);
            Assert.True(enActual.ToList<TestData>().SequenceEqual<TestData>(list));
        }

        [Fact]
        public void TestMalformedDeserialization()
        {
            var arg = new SqlAttribute(string.Empty);
            var converter = new Mock<SqlGenericsConverter<TestData>>(config.Object);

            // SQL data is missing a field
            string json = "[{ \"ID\":1,\"Name\":\"Broom\",\"Timestamp\":\"2019-11-22T06:32:15\"}]";
            converter.Setup(_ => _.BuildItemFromAttribute(arg)).ReturnsAsync(json);
            var list = new List<TestData>();
            var data = new TestData
            {
                ID = 1,
                Name = "Broom",
                Cost = 0,
                Timestamp = new DateTime(2019, 11, 22, 6, 32, 15)
            };
            list.Add(data);
            IEnumerable<TestData> enActual = converter.Object.Convert(arg);
            Assert.True(enActual.ToList<TestData>().SequenceEqual<TestData>(list));

            // SQL data's columns are named differently than the POCO's fields
            json = "[{ \"ID\":1,\"Product Name\":\"Broom\",\"Price\":32.5,\"Timessstamp\":\"2019-11-22T06:32:15\"}]";
            converter.Setup(_ => _.BuildItemFromAttribute(arg)).ReturnsAsync(json);
            list = new List<TestData>();
            data = new TestData
            {
                ID = 1,
                Name = null,
                Cost = 0,
            };
            list.Add(data);
            enActual = converter.Object.Convert(arg);
            Assert.True(enActual.ToList<TestData>().SequenceEqual<TestData>(list));

            // Confirm that the JSON fields are case-insensitive (technically malformed string, but still works)
            json = "[{ \"id\":1,\"nAme\":\"Broom\",\"coSt\":32.5,\"TimEStamp\":\"2019-11-22T06:32:15\"}]";
            converter.Setup(_ => _.BuildItemFromAttribute(arg)).ReturnsAsync(json);
            list = new List<TestData>();
            data = new TestData
            {
                ID = 1,
                Name = "Broom",
                Cost = 32.5,
                Timestamp = new DateTime(2019, 11, 22, 6, 32, 15)
            };
            list.Add(data);
            enActual = converter.Object.Convert(arg);
            Assert.True(enActual.ToList<TestData>().SequenceEqual<TestData>(list));
        }
    }
}
