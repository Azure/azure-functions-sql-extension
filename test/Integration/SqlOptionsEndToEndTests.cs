// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public class SqlOptionsEndToEndTests : SqlTriggerBindingIntegrationTestBase
    {
        [Fact]
        public void ConfigureOptions_AppliesValuesCorrectly_Queues()
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

        /// <summary>
        /// Verifies that manually setting the batch size using the original config var correctly changes the
        /// number of changes processed at once.
        /// </summary>
        [Fact]
        public async Task ConfigOverridesHostOptionsTest()
        {
            string extensionPath = "AzureWebJobs:Extensions:Sql";
            var values = new Dictionary<string, string>
            {
                { $"{extensionPath}:BatchSize", "30" },
                { $"{extensionPath}:PollingIntervalMs", "1000" },
                { $"{extensionPath}:MaxChangesPerWorker", "100" },
            };

            SqlOptions options = TestHelpers.GetConfiguredOptions<SqlOptions>(b =>
            {
                b.AddSql();
            }, values);
            // Use enough items to require 4 batches to be processed but then
            // set the max batch size to the same value so they can all be processed in one
            // batch. The test will only wait for ~1 batch worth of time so will timeout
            // if the max batch size isn't actually changed
            const int maxBatchSize = SqlOptions.DefaultMaxBatchSize * 4;
            const int firstId = 1;
            const int lastId = maxBatchSize;
            this.SetChangeTrackingForTable("Products");
            var taskCompletionSource = new TaskCompletionSource<bool>();
            DataReceivedEventHandler handler = TestUtils.CreateOutputReceievedHandler(
                taskCompletionSource,
                @"Starting change consumption loop. MaxBatchSize: (\d*) PollingIntervalMs: \d*",
                "MaxBatchSize",
                maxBatchSize.ToString());
            this.StartFunctionHost(
                nameof(ProductsTriggerWithValidation),
                SupportedLanguages.CSharp,
                useTestFolder: true,
                customOutputHandler: handler,
                environmentVariables: new Dictionary<string, string>() {
                    { "TEST_EXPECTED_MAX_BATCH_SIZE", maxBatchSize.ToString() },
                    { "Sql_Trigger_BatchSize", maxBatchSize.ToString() } // Use old BatchSize config
                }
            );

            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Insert,
                () => { this.InsertProducts(firstId, lastId); return Task.CompletedTask; },
                id => $"Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId, maxBatchSize: maxBatchSize));
            await taskCompletionSource.Task.TimeoutAfter(TimeSpan.FromSeconds(5), "Timed out waiting for MaxBatchSize configuration message");

            const int pollingIntervalMs = SqlOptions.DefaultPollingIntervalMs / 2;
            this.SetChangeTrackingForTable("Products");
            taskCompletionSource = new TaskCompletionSource<bool>();
            handler = TestUtils.CreateOutputReceievedHandler(
                taskCompletionSource,
                @"Starting change consumption loop. MaxBatchSize: \d* PollingIntervalMs: (\d*)",
                "PollingInterval",
                pollingIntervalMs.ToString());
            this.StartFunctionHost(
                nameof(ProductsTriggerWithValidation),
                SupportedLanguages.CSharp,
                useTestFolder: true,
                customOutputHandler: handler,
                environmentVariables: new Dictionary<string, string>() {
                    { "Sql_Trigger_PollingIntervalMs", pollingIntervalMs.ToString() }
                }
            );

            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Insert,
                () => { this.InsertProducts(firstId, lastId); return Task.CompletedTask; },
                id => $"Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId, pollingIntervalMs: pollingIntervalMs));
            await taskCompletionSource.Task.TimeoutAfter(TimeSpan.FromSeconds(5), "Timed out waiting for PollingInterval configuration message");
        }


    }
}