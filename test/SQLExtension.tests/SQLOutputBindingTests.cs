using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Sql;
using System;
using Xunit;

namespace SQLExtension.tests
{
    public class SQLOutputBindingTests
    {

        [Fact]
        public void TestNullCollectorConstructorArguments()
        {
            var arg = new SqlAttribute(string.Empty);
            var connection = new SqlConnectionWrapper();
            Assert.Throws<ArgumentNullException>(() => new SqlAsyncCollector<string>(connection, null));
            Assert.Throws<ArgumentNullException>(() => new SqlAsyncCollector<string>(null, arg));
        }

        [Fact]
        public void TestNullItem()
        {
            var arg = new SqlAttribute(string.Empty);
            var connection = new SqlConnectionWrapper();
            var collector = new SqlAsyncCollector<string>(connection, arg);
            Assert.ThrowsAsync<ArgumentNullException>(() => collector.AddAsync(null));
        }

        [Fact]
        public void TestAddAsync()
        {
            // Really a pretty silly test. Just confirms that the Sql connection is only opened when FlushAsync is called,
            // because otherwise we would get an exception in AddAsync (since the Sql connection in the wrapper is null)
            var arg = new SqlAttribute(string.Empty);
            var connection = new SqlConnectionWrapper();
            var collector = new SqlAsyncCollector<Data>(connection, arg);
            var data = new Data
            {
                ID = 1,
                Name = "Data",
                Cost = 10,
                Timestamp = new DateTime(2019, 11, 22, 6, 32, 15)
            };
            collector.AddAsync(data);
        }
    }
}
