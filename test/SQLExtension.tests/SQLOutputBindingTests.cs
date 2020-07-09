using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.SQL;
using Moq;
using SQLBindingExtension;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using Xunit;
using static SQLBindingExtension.SQLCollectors;
using static SQLBindingExtension.SQLConverters;

namespace SQLExtension.tests
{
    public class SQLOutputBindingTests
    {

        [Fact]
        public void TestNullCollectorConstructorArguments()
        {
            var arg = new SQLBindingAttribute();
            var connection = new SqlConnectionWrapper();
            Assert.Throws<ArgumentNullException>(() => new SQLAsyncCollector<string>(connection, null));
            Assert.Throws<ArgumentNullException>(() => new SQLCollector<string>(connection, null));
            Assert.Throws<ArgumentNullException>(() => new SQLAsyncCollector<string>(null, arg));
            Assert.Throws<ArgumentNullException>(() => new SQLCollector<string>(null, arg));
        }

        [Fact]
        public void TestNullItem()
        {
            var arg = new SQLBindingAttribute();
            var connection = new SqlConnectionWrapper();
            var asyncCollector = new SQLAsyncCollector<string>(connection, arg);
            var syncCollector = new SQLCollector<string>(connection, arg);
            Assert.ThrowsAsync<ArgumentNullException>(() => asyncCollector.AddAsync(null));
            Assert.Throws<ArgumentNullException>(() => syncCollector.Add(null));
        }

        [Fact]
        public void TestSQLConnectionFailure()
        {
            // Should get an invalid operation exception because the SqlConnection is null, so shouldn't be able to open it.
            // Also confirms that the SQLAsyncCollector only processes the rows once FlushAsync is called, because that's when
            // the exception should be thrown for it
            var arg = new SQLBindingAttribute();
            var connection = new SqlConnectionWrapper();
            var asyncCollector = new SQLAsyncCollector<Data>(connection, arg);
            var syncCollector = new SQLCollector<Data>(connection, arg);
            var data = new Data
            {
                ID = 1,
                Name = "Data",
                Cost = 10,
                Timestamp = new DateTime(2019, 11, 22, 6, 32, 15)
            };
            Assert.Throws<InvalidOperationException>(() => syncCollector.Add(data));
            asyncCollector.AddAsync(data);
            Assert.ThrowsAsync<InvalidOperationException>(() => asyncCollector.FlushAsync());
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
