// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.TriggerBindingSamples;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class SqlTriggerBindingIntegrationTests : IntegrationTestBase
    {
        public SqlTriggerBindingIntegrationTests(ITestOutputHelper output = null) : base(output)
        {
            this.EnableChangeTrackingForDatabase();
        }

        /// <summary>
        /// Ensures that the user function gets invoked for each of the insert, update and delete operation.
        /// </summary>
        [Fact]
        public async Task SingleOperationTriggerTest()
        {
            this.SetChangeTrackingForTable("Products");
            this.StartFunctionHost(nameof(ProductsTrigger), SupportedLanguages.CSharp);

            int firstId = 1;
            int lastId = 30;
            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Insert,
                () => { this.InsertProducts(firstId, lastId); return Task.CompletedTask; },
                id => $"Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId));

            firstId = 1;
            lastId = 20;
            // All table columns (not just the columns that were updated) would be returned for update operation.
            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Update,
                () => { this.UpdateProducts(firstId, lastId); return Task.CompletedTask; },
                id => $"Updated Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId));

            firstId = 11;
            lastId = 30;
            // The properties corresponding to non-primary key columns would be set to the C# type's default values
            // (null and 0) for delete operation.
            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Delete,
                () => { this.DeleteProducts(firstId, lastId); return Task.CompletedTask; },
                _ => null,
                _ => 0,
                this.GetBatchProcessingTimeout(firstId, lastId));
        }

        /// <summary>
        /// Verifies that manually setting the batch size correctly changes the number of changes processed at once
        /// </summary>
        [Fact]
        public async Task BatchSizeOverrideTriggerTest()
        {
            // Use enough items to require 4 batches to be processed but then
            // set the batch size to the same value so they can all be processed in one
            // batch. The test will only wait for ~1 batch worth of time so will timeout
            // if the batch size isn't actually changed
            const int batchSize = SqlTableChangeMonitor<object>.DefaultBatchSize * 4;
            const int firstId = 1;
            const int lastId = batchSize;
            this.SetChangeTrackingForTable("Products");
            var taskCompletionSource = new TaskCompletionSource<bool>();
            DataReceivedEventHandler handler = TestUtils.CreateOutputReceievedHandler(
                taskCompletionSource,
                @"Starting change consumption loop. BatchSize: (\d*) PollingIntervalMs: \d*",
                "BatchSize",
                batchSize.ToString());
            this.StartFunctionHost(
                nameof(ProductsTriggerWithValidation),
                SupportedLanguages.CSharp,
                useTestFolder: true,
                customOutputHandler: handler,
                environmentVariables: new Dictionary<string, string>() {
                    { "TEST_EXPECTED_BATCH_SIZE", batchSize.ToString() },
                    { "Sql_Trigger_BatchSize", batchSize.ToString() }
                }
            );

            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Insert,
                () => { this.InsertProducts(firstId, lastId); return Task.CompletedTask; },
                id => $"Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId, batchSize: batchSize));
            await taskCompletionSource.Task.TimeoutAfter(TimeSpan.FromSeconds(5000), "Timed out waiting for BatchSize configuration message");
        }

        /// <summary>
        /// Verifies that manually setting the polling interval correctly changes the delay between processing each batch of changes
        /// </summary>
        [Fact]
        public async Task PollingIntervalOverrideTriggerTest()
        {
            const int firstId = 1;
            // Use enough items to require 5 batches to be processed - the test will
            // only wait for the expected time and timeout if the default polling
            // interval isn't actually modified.
            const int lastId = SqlTableChangeMonitor<object>.DefaultBatchSize * 5;
            const int pollingIntervalMs = SqlTableChangeMonitor<object>.DefaultPollingIntervalMs / 2;
            this.SetChangeTrackingForTable("Products");
            var taskCompletionSource = new TaskCompletionSource<bool>();
            DataReceivedEventHandler handler = TestUtils.CreateOutputReceievedHandler(
                taskCompletionSource,
                @"Starting change consumption loop. BatchSize: \d* PollingIntervalMs: (\d*)",
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
            await taskCompletionSource.Task.TimeoutAfter(TimeSpan.FromSeconds(5000), "Timed out waiting for PollingInterval configuration message");
        }

        /// <summary>
        /// Verifies that if several changes have happened to the table row since last invocation, then a single net
        /// change for that row is passed to the user function.
        /// </summary>
        [Fact]
        public async Task MultiOperationTriggerTest()
        {
            int firstId = 1;
            int lastId = 5;
            this.SetChangeTrackingForTable("Products");
            this.StartFunctionHost(nameof(ProductsTrigger), SupportedLanguages.CSharp);

            // 1. Insert + multiple updates to a row are treated as single insert with latest row values.
            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Insert,
                () =>
                {
                    this.InsertProducts(firstId, lastId);
                    this.UpdateProducts(firstId, lastId);
                    this.UpdateProducts(firstId, lastId);
                    return Task.CompletedTask;
                },
                id => $"Updated Updated Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId));

            firstId = 6;
            lastId = 10;
            // 2. Multiple updates to a row are treated as single update with latest row values.
            // First insert items and wait for those changes to be sent
            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Insert,
                () =>
                {
                    this.InsertProducts(firstId, lastId);
                    return Task.CompletedTask;
                },
                id => $"Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId));

            firstId = 6;
            lastId = 10;
            // Now do multiple updates at once and verify the updates are batched together
            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Update,
                () =>
                {
                    this.UpdateProducts(firstId, lastId);
                    this.UpdateProducts(firstId, lastId);
                    return Task.CompletedTask;
                },
                id => $"Updated Updated Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId));

            firstId = 11;
            lastId = 20;
            // 3. Insert + (zero or more updates) + delete to a row are treated as single delete with default values for non-primary columns.
            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Delete,
                () =>
                {
                    this.InsertProducts(firstId, lastId);
                    this.UpdateProducts(firstId, lastId);
                    this.DeleteProducts(firstId, lastId);
                    return Task.CompletedTask;
                },
                _ => null,
                _ => 0,
                this.GetBatchProcessingTimeout(firstId, lastId));
        }

        /// <summary>
        /// Ensures correct functionality with multiple user functions tracking the same table.
        /// </summary>
        [Fact]
        public async Task MultiFunctionTriggerTest()
        {
            const string Trigger1Changes = "Trigger1 Changes: ";
            const string Trigger2Changes = "Trigger2 Changes: ";

            this.SetChangeTrackingForTable("Products");

            string functionList = $"{nameof(MultiFunctionTrigger.MultiFunctionTrigger1)} {nameof(MultiFunctionTrigger.MultiFunctionTrigger2)}";
            this.StartFunctionHost(functionList, SupportedLanguages.CSharp, useTestFolder: true);

            // 1. INSERT
            int firstId = 1;
            int lastId = 30;
            // Set up monitoring for Trigger 1...
            Task changes1Task = this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Insert,
                () =>
                {
                    return Task.CompletedTask;
                },
                id => $"Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId),
                Trigger1Changes
                );

            // Set up monitoring for Trigger 2...
            Task changes2Task = this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Insert,
                () =>
                {
                    return Task.CompletedTask;
                },
                id => $"Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId),
                Trigger2Changes
                );

            // Now that monitoring is set up make the changes and then wait for the monitoring tasks to see them and complete
            this.InsertProducts(firstId, lastId);
            await Task.WhenAll(changes1Task, changes2Task);

            // 2. UPDATE
            firstId = 1;
            lastId = 20;
            // All table columns (not just the columns that were updated) would be returned for update operation.
            // Set up monitoring for Trigger 1...
            changes1Task = this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Update,
                () =>
                {
                    return Task.CompletedTask;
                },
                id => $"Updated Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId),
                Trigger1Changes);

            // Set up monitoring for Trigger 2...
            changes2Task = this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Update,
                () =>
                {
                    return Task.CompletedTask;
                },
                id => $"Updated Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId),
                Trigger2Changes);

            // Now that monitoring is set up make the changes and then wait for the monitoring tasks to see them and complete
            this.UpdateProducts(firstId, lastId);
            await Task.WhenAll(changes1Task, changes2Task);

            // 3. DELETE
            firstId = 11;
            lastId = 30;
            // The properties corresponding to non-primary key columns would be set to the C# type's default values
            // (null and 0) for delete operation.
            // Set up monitoring for Trigger 1...
            changes1Task = this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Delete,
                () =>
                {
                    return Task.CompletedTask;
                },
                _ => null,
                _ => 0,
                this.GetBatchProcessingTimeout(firstId, lastId),
                Trigger1Changes);

            // Set up monitoring for Trigger 2...
            changes2Task = this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Delete,
                () =>
                {
                    return Task.CompletedTask;
                },
                _ => null,
                _ => 0,
                this.GetBatchProcessingTimeout(firstId, lastId),
                Trigger2Changes);

            // Now that monitoring is set up make the changes and then wait for the monitoring tasks to see them and complete
            this.DeleteProducts(firstId, lastId);
            await Task.WhenAll(changes1Task, changes2Task);
        }

        /// <summary>
        /// Ensures correct functionality with user functions running across multiple functions host processes.
        /// </summary>
        [Fact]
        public async Task MultiHostTriggerTest()
        {
            this.SetChangeTrackingForTable("Products");

            // Prepare three function host processes.
            this.StartFunctionHost(nameof(ProductsTrigger), SupportedLanguages.CSharp);
            this.StartFunctionHost(nameof(ProductsTrigger), SupportedLanguages.CSharp);
            this.StartFunctionHost(nameof(ProductsTrigger), SupportedLanguages.CSharp);

            int firstId = 1;
            int lastId = 90;
            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Insert,
                () => { this.InsertProducts(firstId, lastId); return Task.CompletedTask; },
                id => $"Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId));

            firstId = 1;
            lastId = 60;
            // All table columns (not just the columns that were updated) would be returned for update operation.
            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Update,
                () => { this.UpdateProducts(firstId, lastId); return Task.CompletedTask; },
                id => $"Updated Product {id}",
                id => id * 100,
                this.GetBatchProcessingTimeout(firstId, lastId));

            firstId = 31;
            lastId = 90;
            // The properties corresponding to non-primary key columns would be set to the C# type's default values
            // (null and 0) for delete operation.
            await this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Delete,
                () => { this.DeleteProducts(firstId, lastId); return Task.CompletedTask; },
                _ => null,
                _ => 0,
                this.GetBatchProcessingTimeout(firstId, lastId));
        }

        /// <summary>
        /// Tests the error message when the user table is not present in the database.
        /// </summary>
        [Fact]
        public void TableNotPresentTriggerTest()
        {
            this.StartFunctionHostAndWaitForError(
                nameof(TableNotPresentTrigger),
                true,
                "Could not find table: 'dbo.TableNotPresent'.");
        }

        /// <summary>
        /// Tests the error message when the user table does not contain primary key.
        /// </summary>
        [Fact]
        public void PrimaryKeyNotCreatedTriggerTest()
        {
            this.StartFunctionHostAndWaitForError(
                nameof(PrimaryKeyNotPresentTrigger),
                true,
                "Could not find primary key created in table: 'dbo.ProductsWithoutPrimaryKey'.");
        }

        /// <summary>
        /// Tests the error message when the user table contains one or more primary keys with names conflicting with
        /// column names in the leases table.
        /// </summary>
        [Fact]
        public void ReservedPrimaryKeyColumnNamesTriggerTest()
        {
            this.StartFunctionHostAndWaitForError(
                nameof(ReservedPrimaryKeyColumnNamesTrigger),
                true,
                "Found reserved column name(s): '_az_func_ChangeVersion', '_az_func_AttemptCount', '_az_func_LeaseExpirationTime' in table: 'dbo.ProductsWithReservedPrimaryKeyColumnNames'." +
                " Please rename them to be able to use trigger binding.");
        }

        /// <summary>
        /// Tests the error message when the user table contains columns of unsupported SQL types.
        /// </summary>
        [Fact]
        public void UnsupportedColumnTypesTriggerTest()
        {
            this.StartFunctionHostAndWaitForError(
                nameof(UnsupportedColumnTypesTrigger),
                true,
                "Found column(s) with unsupported type(s): 'Location' (type: geography), 'Geometry' (type: geometry), 'Organization' (type: hierarchyid)" +
                " in table: 'dbo.ProductsWithUnsupportedColumnTypes'.");
        }

        /// <summary>
        /// Tests the error message when change tracking is not enabled on the user table.
        /// </summary>
        [Fact]
        public void ChangeTrackingNotEnabledTriggerTest()
        {
            this.StartFunctionHostAndWaitForError(
                nameof(ProductsTrigger),
                false,
                "Could not find change tracking enabled for table: 'dbo.Products'.");
        }

        /// <summary>
        /// Tests that the GetMetrics call works correctly.
        /// </summary>
        /// <remarks>We call this directly since there isn't a way to test scaling locally - with this we at least verify the methods called don't throw unexpectedly.</remarks>
        [Fact]
        public async void GetMetricsTest()
        {
            this.SetChangeTrackingForTable("Products");
            IConfiguration configuration = new ConfigurationBuilder().Build();
            var listener = new SqlTriggerListener<Product>(this.DbConnectionString, "dbo.Products", "func-id", Mock.Of<ITriggeredFunctionExecutor>(), Mock.Of<ILogger>(), configuration);
            await listener.StartAsync(CancellationToken.None);
            // Cancel immediately so the listener doesn't start processing the changes
            await listener.StopAsync(CancellationToken.None);
            SqlTriggerMetrics metrics = await listener.GetMetricsAsync();
            Assert.True(metrics.UnprocessedChangeCount == 0, "There should initially be 0 unprocessed changes");
            this.InsertProducts(1, 5);
            metrics = await listener.GetMetricsAsync();
            Assert.True(metrics.UnprocessedChangeCount == 5, $"There should be 5 unprocessed changes after insertion. Actual={metrics.UnprocessedChangeCount}");
        }

        private void EnableChangeTrackingForDatabase()
        {
            this.ExecuteNonQuery($@"
                ALTER DATABASE [{this.DatabaseName}]
                SET CHANGE_TRACKING = ON
                (CHANGE_RETENTION = 2 DAYS, AUTO_CLEANUP = ON);
            ");
        }

        protected void SetChangeTrackingForTable(string tableName, bool enable = true)
        {
            this.ExecuteNonQuery($@"
                ALTER TABLE [dbo].[{tableName}]
                {(enable ? "ENABLE" : "DISABLE")} CHANGE_TRACKING;
            ");
        }

        protected void InsertProducts(int firstId, int lastId)
        {
            // Only 1000 items are allowed to be inserted into a single INSERT statement so if we have more than 1000 batch them up into separate statements
            var builder = new StringBuilder();
            do
            {
                int batchCount = Math.Min(lastId - firstId + 1, 1000);
                builder.Append($"INSERT INTO [dbo].[Products] VALUES {string.Join(",\n", Enumerable.Range(firstId, batchCount).Select(id => $"({id}, 'Product {id}', {id * 100})"))}; ");
                firstId += batchCount;
            } while (firstId < lastId);
            this.ExecuteNonQuery(builder.ToString());
        }

        protected void UpdateProducts(int firstId, int lastId)
        {
            int count = lastId - firstId + 1;
            this.ExecuteNonQuery(
                "UPDATE [dbo].[Products]\n" +
                "SET Name = 'Updated ' + Name\n" +
                "WHERE ProductId IN (" + string.Join(", ", Enumerable.Range(firstId, count)) + ");");
        }

        protected void DeleteProducts(int firstId, int lastId)
        {
            int count = lastId - firstId + 1;
            this.ExecuteNonQuery(
                "DELETE FROM [dbo].[Products]\n" +
                "WHERE ProductId IN (" + string.Join(", ", Enumerable.Range(firstId, count)) + ");");
        }

        protected async Task WaitForProductChanges(
            int firstId,
            int lastId,
            SqlChangeOperation operation,
            Func<Task> actions,
            Func<int, string> getName,
            Func<int, int> getCost,
            int timeoutMs,
            string messagePrefix = "SQL Changes: ")
        {
            var expectedIds = Enumerable.Range(firstId, lastId - firstId + 1).ToHashSet();
            int index = 0;

            var taskCompletion = new TaskCompletionSource<bool>();

            void MonitorOutputData(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null && (index = e.Data.IndexOf(messagePrefix, StringComparison.Ordinal)) >= 0)
                {
                    string json = e.Data[(index + messagePrefix.Length)..];
                    IReadOnlyList<SqlChange<Product>> changes = JsonConvert.DeserializeObject<IReadOnlyList<SqlChange<Product>>>(json);
                    foreach (SqlChange<Product> change in changes)
                    {
                        Assert.Equal(operation, change.Operation); // Expected change operation
                        Product product = change.Item;
                        Assert.NotNull(product); // Product deserialized correctly
                        Assert.Contains(product.ProductID, expectedIds); // We haven't seen this product ID yet, and it's one we expected to see
                        expectedIds.Remove(product.ProductID);
                        Assert.Equal(getName(product.ProductID), product.Name); // The product has the expected name
                        Assert.Equal(getCost(product.ProductID), product.Cost); // The product has the expected cost
                    }
                    if (expectedIds.Count == 0)
                    {
                        taskCompletion.SetResult(true);
                    }
                }
            };
            // Set up listener for the changes coming in
            foreach (Process functionHost in this.FunctionHostList)
            {
                functionHost.OutputDataReceived += MonitorOutputData;
            }

            // Now that we've set up our listener trigger the actions to monitor
            await actions();

            // Now wait until either we timeout or we've gotten all the expected changes, whichever comes first
            Console.WriteLine($"[{DateTime.UtcNow:u}] Waiting for {operation} changes ({timeoutMs}ms)");
            await taskCompletion.Task.TimeoutAfter(TimeSpan.FromMilliseconds(timeoutMs), $"Timed out waiting for {operation} changes.");

            // Unhook handler since we're done monitoring these changes so we aren't checking other changes done later
            foreach (Process functionHost in this.FunctionHostList)
            {
                functionHost.OutputDataReceived -= MonitorOutputData;
            }
        }

        /// <summary>
        /// Launches the functions runtime host, waits for it to encounter error while starting the SQL trigger listener,
        /// and asserts that the logged error message matches with the supplied error message.
        /// </summary>
        /// <param name="functionName">Name of the user function that should cause error in trigger listener</param>
        /// <param name="useTestFolder">Whether the functions host should be launched from test folder</param>
        /// <param name="expectedErrorMessage">Expected error message string</param>
        private void StartFunctionHostAndWaitForError(string functionName, bool useTestFolder, string expectedErrorMessage)
        {
            string errorMessage = null;
            var tcs = new TaskCompletionSource<bool>();

            void OutputHandler(object sender, DataReceivedEventArgs e)
            {
                if (errorMessage == null && e.Data?.Contains("Failed to start SQL trigger listener") == true)
                {
                    // SQL trigger listener throws exception of type InvalidOperationException for all error conditions.
                    string exceptionPrefix = "Exception: System.InvalidOperationException: ";
                    int index = e.Data.IndexOf(exceptionPrefix, StringComparison.Ordinal);
                    Assert.NotEqual(-1, index);

                    errorMessage = e.Data[(index + exceptionPrefix.Length)..];
                    tcs.SetResult(true);
                }
            };

            // All trigger integration tests are only using C# functions for testing at the moment.
            this.StartFunctionHost(functionName, SupportedLanguages.CSharp, useTestFolder, OutputHandler);

            // The functions host generally logs the error message within a second after starting up.
            const int BufferTimeForErrorInSeconds = 15;
            bool isCompleted = tcs.Task.Wait(TimeSpan.FromSeconds(BufferTimeForErrorInSeconds));

            this.FunctionHost.OutputDataReceived -= OutputHandler;
            this.FunctionHost.Kill(true);

            Assert.True(isCompleted, "Functions host did not log failure to start SQL trigger listener within specified time.");
            Assert.Equal(expectedErrorMessage, errorMessage);
        }

        /// <summary>
        /// Gets a timeout value to use when processing the given number of changes, based on the
        /// default batch size and polling interval.
        /// </summary>
        /// <param name="firstId">The first ID in the batch to process</param>
        /// <param name="lastId">The last ID in the batch to process</param>
        /// <param name="batchSize">The batch size if different than the default batch size</param>
        /// <param name="pollingIntervalMs">The polling interval in ms if different than the default polling interval</param>
        /// <returns></returns>
        protected int GetBatchProcessingTimeout(int firstId, int lastId, int batchSize = SqlTableChangeMonitor<object>.DefaultBatchSize, int pollingIntervalMs = SqlTableChangeMonitor<object>.DefaultPollingIntervalMs)
        {
            int changesToProcess = lastId - firstId + 1;
            int calculatedTimeout = (int)(Math.Ceiling((double)changesToProcess / batchSize // The number of batches to process
                / this.FunctionHostList.Count) // The number of function host processes
                * pollingIntervalMs // The length to process each batch
                * 2); // Double to add buffer time for processing results & writing log messages

            // Always have a timeout of at least 10sec since there's a certain amount of overhead
            // always expected from each run regardless of the number of batches being processed and the delay
            // These tests aren't testing performance so giving extra processing time is fine as long as the
            // results themselves are correct
            return Math.Max(calculatedTimeout, 10000);
        }
    }
}