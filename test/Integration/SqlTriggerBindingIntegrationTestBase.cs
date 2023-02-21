// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    [LogTestName]
    public class SqlTriggerBindingIntegrationTestBase : IntegrationTestBase
    {
        public SqlTriggerBindingIntegrationTestBase(ITestOutputHelper output = null) : base(output)
        {
            this.EnableChangeTrackingForDatabase();
        }

        private void EnableChangeTrackingForDatabase()
        {
            this.ExecuteNonQuery($@"
                ALTER DATABASE [{this.DatabaseName}]
                SET CHANGE_TRACKING = ON
                (CHANGE_RETENTION = 2 DAYS, AUTO_CLEANUP = ON);
            ");
        }

        public void SetChangeTrackingForTable(string tableName, bool enable = true)
        {
            this.ExecuteNonQuery($@"
                ALTER TABLE [dbo].[{tableName}]
                {(enable ? "ENABLE" : "DISABLE")} CHANGE_TRACKING;
            ");
        }

        public void InsertProducts(int firstId, int lastId)
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

        public async Task WaitForProductChanges(
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
                    // Sometimes we'll get messages that have extra logging content on the same line - so to prevent that from breaking
                    // the deserialization we look for the end of the changes array and only use that.
                    // (This is fine since we control what content is in the array so know that none of the items have a ] in them)
                    json = json[..(json.IndexOf(']') + 1)];
                    IReadOnlyList<SqlChange<Product>> changes;
                    try
                    {
                        changes = JsonConvert.DeserializeObject<IReadOnlyList<SqlChange<Product>>>(json);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Exception deserializing JSON content. Error={ex.Message} Json=\"{json}\"", ex);
                    }
                    foreach (SqlChange<Product> change in changes)
                    {
                        Assert.Equal(operation, change.Operation); // Expected change operation
                        Product product = change.Item;
                        Assert.NotNull(product); // Product deserialized correctly
                        Assert.Contains(product.ProductId, expectedIds); // We haven't seen this product ID yet, and it's one we expected to see
                        expectedIds.Remove(product.ProductId);
                        Assert.Equal(getName(product.ProductId), product.Name); // The product has the expected name
                        Assert.Equal(getCost(product.ProductId), product.Cost); // The product has the expected cost
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
            this.LogOutput($"[{DateTime.UtcNow:u}] Waiting for {operation} changes ({timeoutMs}ms)");
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
        protected void StartFunctionHostAndWaitForError(string functionName, bool useTestFolder, string expectedErrorMessage)
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
        /// default max batch size and polling interval.
        /// </summary>
        /// <param name="firstId">The first ID in the batch to process</param>
        /// <param name="lastId">The last ID in the batch to process</param>
        /// <param name="maxBatchSize">The max batch size if different than the default max batch size</param>
        /// <param name="pollingIntervalMs">The polling interval in ms if different than the default polling interval</param>
        /// <returns></returns>
        public int GetBatchProcessingTimeout(int firstId, int lastId, int maxBatchSize = SqlTableChangeMonitor<object>.DefaultMaxBatchSize, int pollingIntervalMs = SqlTableChangeMonitor<object>.DefaultPollingIntervalMs)
        {
            int changesToProcess = lastId - firstId + 1;
            int calculatedTimeout = (int)(Math.Ceiling((double)changesToProcess / maxBatchSize // The number of batches to process
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