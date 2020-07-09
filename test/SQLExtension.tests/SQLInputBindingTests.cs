using Moq;
using System;
using Xunit;
using SQLBindingExtension;
using System.Collections.Generic;
using System.Linq;
using static SQLBindingExtension.SQLConverters;

namespace Microsoft.Azure.WebJobs.Extensions.SQL.Tests
{
    public class SQLInputBindingTests
    {
        private static string connectionString = "connectionString";

        [Fact]
        public void TestNullConnectionString()
        {
            var arg = new SQLBindingAttribute();
            var connection = new SqlConnectionWrapper();
            var converter = new SQLGenericsConverter<string>(connection);
            Assert.Throws<ArgumentNullException>(() => converter.BuildItemFromAttribute(arg));
        }

       
        [Fact]
        public void TestNullContext()
        {
            var connection = new SqlConnectionWrapper();
            var configProvider = new SQLBindingConfigProvider();
            Assert.Throws<ArgumentNullException>(() => configProvider.Initialize(null));
        }

        [Fact]
        public void TestNullBuilder()
        {
            IWebJobsBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddSQLBinding());
        }

        [Fact]
        public void TestMalformedAuthenticationString()
        {
            var arg = new SQLBindingAttribute();
            arg.ConnectionString = connectionString;
            var connection = new SqlConnectionWrapper();
            var converter = new SQLGenericsConverter<string>(connection);

            // User ID identified incorrectly
            arg.Authentication = "UserID=test;Password=test;";
            var exception = Assert.Throws<ArgumentException>(() => converter.BuildItemFromAttribute(arg));
            Assert.Equal("User ID must be specified in the Authentication string as \"User ID =<userid>;\"", exception.Message);

            // Password identified incorrectly
            arg.Authentication = "User ID=test;Passwrd=test;";
            exception = Assert.Throws<ArgumentException>(() => converter.BuildItemFromAttribute(arg));
            Assert.Equal("Password must be specified in the Authentication string as \"Password =<password>;\"", exception.Message);

            // Forgot semicolon delimiter between User ID and Password
            arg.Authentication = "User ID=testPassword=test;";
            exception = Assert.Throws<ArgumentException>(() => converter.BuildItemFromAttribute(arg));
            Assert.Equal("Keys must be separated by \";\" and key and value must be separated by \"=\", i.e. " +
                        "\"User ID =<userid>;Password =<password>;\"", exception.Message);

            // Didn't include password
            arg.Authentication = "User ID=test;";
            exception = Assert.Throws<ArgumentException>(() => converter.BuildItemFromAttribute(arg));
            Assert.Equal("Password must be specified in the Authentication string as \"Password =<password>;\"", exception.Message);

            // Forgot equals sign after Password
            arg.Authentication = "User ID=test;Passwordtest;";
            exception = Assert.Throws<ArgumentException>(() => converter.BuildItemFromAttribute(arg));
            Assert.Equal("Keys must be separated by \";\" and key and value must be separated by \"=\", i.e. " +
                        "\"User ID =<userid>;Password =<password>;\"", exception.Message);
        }

        [Fact]
        public void TestSQLConnectionFailure()
        {
            // Should get an invalid operation exception because the SqlConnection is null, so shouldn't be able to open it.
            // Also confirms that if the authentication string is null, we don't attempt to parse it (otherwise we would get another exception
            // thrown here)
            var arg = new SQLBindingAttribute();
            arg.ConnectionString = connectionString;
            var connection = new SqlConnectionWrapper();
            var converter = new SQLGenericsConverter<string>(connection);
            Assert.Throws<InvalidOperationException>(() => converter.BuildItemFromAttribute(arg));
        }

        [Fact]
        public void TestWellformedAuthenticationString()
        {
            var arg = new SQLBindingAttribute();
            arg.ConnectionString = connectionString;
            var connection = new SqlConnectionWrapper();
            var converter = new SQLGenericsConverter<string>(connection);

            // Make sure that authentication works even without semicolon at the end. In that case exception should be thrown when the connection
            // is opened
            arg.Authentication = "User ID=test;Password=test";
            Assert.Throws<InvalidOperationException>(() => converter.BuildItemFromAttribute(arg));
        }

        [Fact]
        public void TestWellformedDeserialization()
        {
            var arg = new SQLBindingAttribute();
            var connection = new SqlConnectionWrapper();
            var converter = new Mock<SQLGenericsConverter<Data>>(connection);
            string json = "[{ \"ID\":1,\"Name\":\"Broom\",\"Cost\":32.5,\"Timestamp\":\"2019-11-22T06:32:15\"},{ \"ID\":2,\"Name\":\"Brush\",\"Cost\":12.3," +
                "\"Timestamp\":\"2017-01-27T03:13:11\"},{ \"ID\":3,\"Name\":\"Comb\",\"Cost\":100.12,\"Timestamp\":\"1997-05-03T10:11:56\"}]";
            converter.Setup(_ => _.BuildItemFromAttribute(arg)).Returns(json);
            var list = new List<Data>();
            var data1 = new Data
            {
                ID = 1,
                Name = "Broom",
                Cost = 32.5,
                Timestamp = new DateTime(2019, 11, 22, 6, 32, 15)
            };
            var data2 = new Data
            {
                ID = 2,
                Name = "Brush",
                Cost = 12.3,
                Timestamp = new DateTime(2017, 1, 27, 3, 13, 11)
            };
            var data3 = new Data()
            {
                ID = 3,
                Name = "Comb",
                Cost = 100.12,
                Timestamp = new DateTime(1997, 5, 3, 10, 11, 56)
            };
            list.Add(data1);
            list.Add(data2);
            list.Add(data3);
            IEnumerable<Data> enActual = converter.Object.Convert(arg);
            Assert.True(enActual.ToList<Data>().SequenceEqual<Data>(list));
        }

        [Fact]
        public void TestMalformedDeserialization()
        {
            var arg = new SQLBindingAttribute();
            var connection = new SqlConnectionWrapper();
            var converter = new Mock<SQLGenericsConverter<Data>>(connection);

            // SQL data is missing a field
            string json = "[{ \"ID\":1,\"Name\":\"Broom\",\"Timestamp\":\"2019-11-22T06:32:15\"}]";
            converter.Setup(_ => _.BuildItemFromAttribute(arg)).Returns(json);
            var list = new List<Data>();
            var data = new Data
            {
                ID = 1,
                Name = "Broom",
                Cost = 0,
                Timestamp = new DateTime(2019, 11, 22, 6, 32, 15)
            };
            list.Add(data);
            IEnumerable<Data> enActual = converter.Object.Convert(arg);
            Assert.True(enActual.ToList<Data>().SequenceEqual<Data>(list));

            // SQL data's columns are named differently than the POCO's fields
            json = "[{ \"ID\":1,\"Product Name\":\"Broom\",\"Price\":32.5,\"Timessstamp\":\"2019-11-22T06:32:15\"}]";
            converter.Setup(_ => _.BuildItemFromAttribute(arg)).Returns(json);
            list = new List<Data>();
            data = new Data
            {
                ID = 1,
                Name = null,
                Cost = 0,
            };
            list.Add(data);
            enActual = converter.Object.Convert(arg);
            Assert.True(enActual.ToList<Data>().SequenceEqual<Data>(list));

            // Confirm that the JSON fields are case-insensitive (technically malformed string, but still works)
            json = "[{ \"id\":1,\"nAme\":\"Broom\",\"coSt\":32.5,\"TimEStamp\":\"2019-11-22T06:32:15\"}]";
            converter.Setup(_ => _.BuildItemFromAttribute(arg)).Returns(json);
            list = new List<Data>();
            data = new Data
            {
                ID = 1,
                Name = "Broom",
                Cost = 32.5,
                Timestamp = new DateTime(2019, 11, 22, 6, 32, 15)
            };
            list.Add(data);
            enActual = converter.Object.Convert(arg);
            Assert.True(enActual.ToList<Data>().SequenceEqual<Data>(list));
        }

        public class Data
        {
            public int ID { get; set; }

            public string Name { get; set; }

            public double Cost { get; set; }

            public DateTime Timestamp { get; set; }

            public override bool Equals(object obj)
            {
                var otherData = obj as Data;
                if (otherData == null)
                {
                    return false;
                }
                return this.ID == otherData.ID && this.Cost == otherData.Cost && ((this.Name == null && otherData.Name == null) || this.Name.Equals(otherData.Name))
                    && this.Timestamp.Equals(otherData.Timestamp);
            }
        }
    }
}
