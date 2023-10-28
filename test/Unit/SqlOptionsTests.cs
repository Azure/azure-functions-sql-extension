// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlOptionsTests
    {
        [Fact]
        public void Constructor_Defaults()
        {
            var options = new SqlOptions();

            Assert.Equal(100, options.BatchSize);
            Assert.Equal(1000, options.PollingIntervalMs);
            Assert.Equal(1000, options.MaxChangesPerWorker);
        }

        [Fact]
        public void NewOptions_CanGetAndSetValue()
        {
            var options = new SqlOptions();

            Assert.Equal(100, options.BatchSize);
            options.BatchSize = 200;
            Assert.Equal(200, options.BatchSize);

            Assert.Equal(1000, options.PollingIntervalMs);
            options.PollingIntervalMs = 2000;
            Assert.Equal(2000, options.PollingIntervalMs);

            Assert.Equal(1000, options.MaxChangesPerWorker);
            options.MaxChangesPerWorker = 200;
            Assert.Equal(200, options.MaxChangesPerWorker);
        }

        [Fact]
        public void JsonSerialization()
        {
            var jo = new JObject
            {
                { "BatchSize", 10 },
                { "PollingIntervalMs", 2000 },
                { "MaxChangesPerWorker", 10}
            };
            SqlOptions options = jo.ToObject<SqlOptions>();

            Assert.Equal(10, options.BatchSize);
            Assert.Equal(2000, options.PollingIntervalMs);
            Assert.Equal(10, options.MaxChangesPerWorker);
        }
    }
}
