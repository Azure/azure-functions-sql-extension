using Moq;
using System;
using Xunit;
using Microsoft.Azure.WebJobs.Extensions.SQL;
using System.Data.SqlClient;
using SQLBindingExtension;

namespace Microsoft.Azure.WebJobs.Extensions.SQL.Tests
{
    public class SQLInputBindingTests
    {
        private static string connectionString = "connectionString";

        [Fact]
        public void TestNullConnectionString()
        {
            var arg = new SQLBindingAttribute();
            var connection = new Mock<SqlConnectionWrapper>();
            var configProvider = new SQLBindingConfigProvider(connection.Object);
            Assert.Throws<ArgumentNullException>(() => configProvider.BuildItemFromAttribute(arg));
        }

        [Fact]
        public void TestNullContext()
        {
            var connection = new Mock<SqlConnectionWrapper>();
            var configProvider = new SQLBindingConfigProvider(connection.Object);
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
            var connection = new Mock<SqlConnectionWrapper>();
            var configProvider = new SQLBindingConfigProvider(connection.Object);

            // User ID identified incorrectly
            arg.Authentication = "UserID=test;Password=test;";
            var exception = Assert.Throws<ArgumentException>(() => configProvider.BuildItemFromAttribute(arg));
            Assert.Equal("User ID must be specified in the Authentication string as \"User ID =<userid>;\"", exception.Message);

            // Password identified incorrectly
            arg.Authentication = "User ID=test;Passwrd=test;";
            exception = Assert.Throws<ArgumentException>(() => configProvider.BuildItemFromAttribute(arg));
            Assert.Equal("Password must be specified in the Authentication string as \"Password =<password>;\"", exception.Message);

            // Forgot semicolon delimiter between User ID and Password
            arg.Authentication = "User ID=testPassword=test;";
            exception = Assert.Throws<ArgumentException>(() => configProvider.BuildItemFromAttribute(arg));
            Assert.Equal("Keys must be separated by \";\" and key and value must be separated by \"=\", i.e. " +
                        "\"User ID =<userid>;Password =<password>;\"", exception.Message);

            // Didn't include password
            arg.Authentication = "User ID=test;";
            exception = Assert.Throws<ArgumentException>(() => configProvider.BuildItemFromAttribute(arg));
            Assert.Equal("Password must be specified in the Authentication string as \"Password =<password>;\"", exception.Message);

            // Forgot equals sign after Password
            arg.Authentication = "User ID=test;Passwordtest;";
            exception = Assert.Throws<ArgumentException>(() => configProvider.BuildItemFromAttribute(arg));
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
            var connection = new Mock<SqlConnectionWrapper>();
            var configProvider = new SQLBindingConfigProvider(connection.Object);
            Assert.Throws<InvalidOperationException>(() => configProvider.BuildItemFromAttribute(arg));
        }

        [Fact]
        public void TestWellformedAuthenticationString()
        {
            var arg = new SQLBindingAttribute();
            arg.ConnectionString = connectionString;
            var connection = new Mock<SqlConnectionWrapper>();
            var configProvider = new SQLBindingConfigProvider(connection.Object);

            // Make sure that authentication works even without semicolon at the end. In that case exception should be thrown when the connection
            // is opened
            arg.Authentication = "User ID=test;Password=test";
            Assert.Throws<InvalidOperationException>(() => configProvider.BuildItemFromAttribute(arg));
        }
    }
}
