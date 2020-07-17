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
        public void TestSQLConnectionFailure()
        {
            // Should get an invalid operation exception because the SqlConnection is null, so shouldn't be able to open it.
            // Also confirms that the SqlAsyncCollector only processes the rows once FlushAsync is called, because that's when
            // the exception should be thrown for it
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
            Assert.ThrowsAsync<InvalidOperationException>(() => collector.FlushAsync());
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
