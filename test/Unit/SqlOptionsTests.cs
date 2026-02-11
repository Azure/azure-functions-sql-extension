// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using System;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlOptionsTests
    {
        [Fact]
        public void Constructor_Defaults()
        {
            var options = new SqlOptions();

            Assert.Equal(100, options.MaxBatchSize);
            Assert.Equal(1000, options.PollingIntervalMs);
            Assert.Equal(1000, options.MaxChangesPerWorker);
            Assert.Equal(30000, options.AppLockTimeoutMs);
        }

        [Fact]
        public void NewOptions_CanGetAndSetValue()
        {
            var options = new SqlOptions();

            Assert.Equal(100, options.MaxBatchSize);
            options.MaxBatchSize = 200;
            Assert.Equal(200, options.MaxBatchSize);

            Assert.Equal(1000, options.PollingIntervalMs);
            options.PollingIntervalMs = 2000;
            Assert.Equal(2000, options.PollingIntervalMs);

            Assert.Equal(1000, options.MaxChangesPerWorker);
            options.MaxChangesPerWorker = 200;
            Assert.Equal(200, options.MaxChangesPerWorker);

            Assert.Equal(30000, options.AppLockTimeoutMs);
            options.AppLockTimeoutMs = 60000;
            Assert.Equal(60000, options.AppLockTimeoutMs);
        }

        [Fact]
        public void JsonSerialization()
        {
            var jo = new JObject
            {
                { "MaxBatchSize", 10 },
                { "PollingIntervalMs", 2000 },
                { "MaxChangesPerWorker", 10},
                { "AppLockTimeoutMs", 5000}
            };
            SqlOptions options = jo.ToObject<SqlOptions>();

            Assert.Equal(10, options.MaxBatchSize);
            Assert.Equal(2000, options.PollingIntervalMs);
            Assert.Equal(10, options.MaxChangesPerWorker);
            Assert.Equal(5000, options.AppLockTimeoutMs);
        }

        [Fact]
        public void AppLockTimeoutMs_ThrowsOnTooLowValue()
        {
            var options = new SqlOptions();
            Assert.Throws<ArgumentException>(() => options.AppLockTimeoutMs = 500);
        }

        [Fact]
        public void AppLockTimeoutMs_AcceptsMinimumValue()
        {
            var options = new SqlOptions
            {
                AppLockTimeoutMs = 1000
            };
            Assert.Equal(1000, options.AppLockTimeoutMs);
        }
    }
}
