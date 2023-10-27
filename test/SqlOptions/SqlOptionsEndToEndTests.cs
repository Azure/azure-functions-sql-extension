// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlOptionsEndToEndTests
    {
        [Fact]
        public void ConfigureOptions_AppliesValuesCorrectly_Queues()
        {
            string extensionPath = "AzureWebJobs:Extensions:Sql";
            var values = new System.Collections.Generic.Dictionary<string, string>
            {
                { $"{extensionPath}:BatchSize", "30" },
                { $"{extensionPath}:PollingIntervalMs", "1000" },
                { $"{extensionPath}:MaxChangesPerWorker", "100" },
            };

            SqlOptions options = TestHelpers.GetConfiguredOptions<SqlOptions>(b =>
            {
                b.AddSql();

                /*  _ = b.Services.AddOptions<SqlOptions>().Configure(options =>
                     {
                         options.BatchSize = 30;
                         options.PollingIntervalMs = 1000;
                         options.MaxChangesPerWorker = 100;
                     }); */
            }, values);

            Assert.Equal(30, options.BatchSize);
            Assert.Equal(1000, options.PollingIntervalMs);
            Assert.Equal(100, options.MaxChangesPerWorker);
        }
    }
}