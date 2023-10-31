// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlOptionsEndToEndTests
    {
        [Fact]
        public void ConfigureOptions_AppliesValuesCorrectly_Sql()
        {
            string extensionPath = "AzureWebJobs:Extensions:Sql";
            var values = new Dictionary<string, string>
            {
                { $"{extensionPath}:MaxBatchSize", "30" },
                { $"{extensionPath}:PollingIntervalMs", "1000" },
                { $"{extensionPath}:MaxChangesPerWorker", "100" },
            };

            SqlOptions options = TestHelpers.GetConfiguredOptions<SqlOptions>(b =>
            {
                b.AddSql();
            }, values);

            Assert.Equal(30, options.MaxBatchSize);
            Assert.Equal(1000, options.PollingIntervalMs);
            Assert.Equal(100, options.MaxChangesPerWorker);
        }
    }
}