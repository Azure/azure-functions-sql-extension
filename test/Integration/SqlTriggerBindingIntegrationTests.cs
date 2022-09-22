// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.TriggerBindingSamples;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    [Collection("IntegrationTests")]
    public class SqlTriggerBindingIntegrationTests : IntegrationTestBase
    {
        public SqlTriggerBindingIntegrationTests(ITestOutputHelper output) : base(output)
        {
            this.EnableChangeTrackingForDatabase();
        }

        /// <summary>
        /// Ensures that the user function gets invoked for each of the insert, update and delete operation.
        /// </summary>
        [Fact]
        public async Task SingleOperationTriggerTest()
        {
            this.EnableChangeTrackingForTable("Products");
            this.StartFunctionHost(nameof(ProductsTrigger), Common.SupportedLanguages.CSharp);

            var changes = new List<SqlChange<Product>>();
            this.MonitorProductChanges(changes, "SQL Changes: ");

            // Considering the polling interval of 5 seconds and batch-size of 10, it should take around 15 seconds to
            // process 30 insert operations. Similar reasoning is used to set delays for update and delete operations.
            this.InsertProducts(1, 30);
            await Task.Delay(TimeSpan.FromSeconds(20));
            ValidateProductChanges(changes, 1, 30, SqlChangeOperation.Insert, id => $"Product {id}", id => id * 100);
            changes.Clear();

            // All table columns (not just the columns that were updated) would be returned for update operation.
            this.UpdateProducts(1, 20);
            await Task.Delay(TimeSpan.FromSeconds(15));
            ValidateProductChanges(changes, 1, 20, SqlChangeOperation.Update, id => $"Updated Product {id}", id => id * 100);
            changes.Clear();

            // The properties corresponding to non-primary key columns would be set to the C# type's default values
            // (null and 0) for delete operation.
            this.DeleteProducts(11, 30);
            await Task.Delay(TimeSpan.FromSeconds(15));
            ValidateProductChanges(changes, 11, 30, SqlChangeOperation.Delete, _ => null, _ => 0);
            changes.Clear();
        }

        /// <summary>
        /// Verifies that manually setting the batch size correctly changes the number of changes processed at once
        /// </summary>
        [Fact]
        public async Task BatchSizeOverrideTriggerTest()
        {
            this.EnableChangeTrackingForTable("Products");
            this.StartFunctionHost(nameof(ProductsTriggerWithValidation), Common.SupportedLanguages.CSharp, true, environmentVariables: new Dictionary<string, string>() {
                { "TEST_EXPECTED_BATCH_SIZE", "20" },
                { "Sql_Trigger_BatchSize", "20" }
            });

            var changes = new List<SqlChange<Product>>();
            this.MonitorProductChanges(changes, "SQL Changes: ");

            // Considering the polling interval of 5 seconds and batch-size of 20, it should take around 10 seconds to
            // process 40 insert operations.
            this.InsertProducts(1, 40);
            await Task.Delay(TimeSpan.FromSeconds(12));
            ValidateProductChanges(changes, 1, 40, SqlChangeOperation.Insert, id => $"Product {id}", id => id * 100);
        }

        /// <summary>
        /// Verifies that manually setting the polling interval correctly changes the delay between processing each batch of changes
        /// </summary>
        [Fact]
        public async Task PollingIntervalOverrideTriggerTest()
        {
            this.EnableChangeTrackingForTable("Products");
            this.StartFunctionHost(nameof(ProductsTriggerWithValidation), Common.SupportedLanguages.CSharp, true, environmentVariables: new Dictionary<string, string>() {
                { "Sql_Trigger_PollingIntervalMs", "100" }
            });

            var changes = new List<SqlChange<Product>>();
            this.MonitorProductChanges(changes, "SQL Changes: ");

            // Considering the polling interval of 100ms and batch-size of 10, it should take around .5 second to
            // process 50 insert operations.
            this.InsertProducts(1, 50);
            await Task.Delay(TimeSpan.FromSeconds(1));
            ValidateProductChanges(changes, 1, 50, SqlChangeOperation.Insert, id => $"Product {id}", id => id * 100);
        }


        /// <summary>
        /// Verifies that if several changes have happened to the table row since last invocation, then a single net
        /// change for that row is passed to the user function.
        /// </summary>
        [Fact]
        public async Task MultiOperationTriggerTest()
        {
            this.EnableChangeTrackingForTable("Products");
            this.StartFunctionHost(nameof(ProductsTrigger), Common.SupportedLanguages.CSharp);

            var changes = new List<SqlChange<Product>>();
            this.MonitorProductChanges(changes, "SQL Changes: ");

            // Insert + multiple updates to a row are treated as single insert with latest row values.
            this.InsertProducts(1, 5);
            this.UpdateProducts(1, 5);
            this.UpdateProducts(1, 5);
            await Task.Delay(TimeSpan.FromSeconds(6));
            ValidateProductChanges(changes, 1, 5, SqlChangeOperation.Insert, id => $"Updated Updated Product {id}", id => id * 100);
            changes.Clear();

            // Multiple updates to a row are treated as single update with latest row values.
            this.InsertProducts(6, 10);
            await Task.Delay(TimeSpan.FromSeconds(6));
            changes.Clear();
            this.UpdateProducts(6, 10);
            this.UpdateProducts(6, 10);
            await Task.Delay(TimeSpan.FromSeconds(6));
            ValidateProductChanges(changes, 6, 10, SqlChangeOperation.Update, id => $"Updated Updated Product {id}", id => id * 100);
            changes.Clear();

            // Insert + (zero or more updates) + delete to a row are treated as single delete with default values for non-primary columns.
            this.InsertProducts(11, 20);
            this.UpdateProducts(11, 20);
            this.DeleteProducts(11, 20);
            await Task.Delay(TimeSpan.FromSeconds(6));
            ValidateProductChanges(changes, 11, 20, SqlChangeOperation.Delete, _ => null, _ => 0);
            changes.Clear();
        }


        /// <summary>
        /// Ensures correct functionality with multiple user functions tracking the same table.
        /// </summary>
        [Fact]
        public async Task MultiFunctionTriggerTest()
        {
            this.EnableChangeTrackingForTable("Products");

            string functionList = $"{nameof(MultiFunctionTrigger.MultiFunctionTrigger1)} {nameof(MultiFunctionTrigger.MultiFunctionTrigger2)}";
            this.StartFunctionHost(functionList, Common.SupportedLanguages.CSharp, useTestFolder: true);

            var changes1 = new List<SqlChange<Product>>();
            var changes2 = new List<SqlChange<Product>>();

            this.MonitorProductChanges(changes1, "Trigger1 Changes: ");
            this.MonitorProductChanges(changes2, "Trigger2 Changes: ");

            // Considering the polling interval of 5 seconds and batch-size of 10, it should take around 15 seconds to
            // process 30 insert operations for each trigger-listener. Similar reasoning is used to set delays for
            // update and delete operations.
            this.InsertProducts(1, 30);
            await Task.Delay(TimeSpan.FromSeconds(20));
            ValidateProductChanges(changes1, 1, 30, SqlChangeOperation.Insert, id => $"Product {id}", id => id * 100);
            ValidateProductChanges(changes2, 1, 30, SqlChangeOperation.Insert, id => $"Product {id}", id => id * 100);
            changes1.Clear();
            changes2.Clear();

            // All table columns (not just the columns that were updated) would be returned for update operation.
            this.UpdateProducts(1, 20);
            await Task.Delay(TimeSpan.FromSeconds(15));
            ValidateProductChanges(changes1, 1, 20, SqlChangeOperation.Update, id => $"Updated Product {id}", id => id * 100);
            ValidateProductChanges(changes2, 1, 20, SqlChangeOperation.Update, id => $"Updated Product {id}", id => id * 100);
            changes1.Clear();
            changes2.Clear();

            // The properties corresponding to non-primary key columns would be set to the C# type's default values
            // (null and 0) for delete operation.
            this.DeleteProducts(11, 30);
            await Task.Delay(TimeSpan.FromSeconds(15));
            ValidateProductChanges(changes1, 11, 30, SqlChangeOperation.Delete, _ => null, _ => 0);
            ValidateProductChanges(changes2, 11, 30, SqlChangeOperation.Delete, _ => null, _ => 0);
            changes1.Clear();
            changes2.Clear();
        }

        /// <summary>
        /// Tests the error message when the user table is not present in the database.
        /// </summary>
        [Fact]
        public void TableNotPresentTriggerTest()
        {
            this.StartFunctionsHostAndWaitForError(
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
            this.StartFunctionsHostAndWaitForError(
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
            this.StartFunctionsHostAndWaitForError(
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
            this.StartFunctionsHostAndWaitForError(
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
            this.StartFunctionsHostAndWaitForError(
                nameof(ProductsTrigger),
                false,
                "Could not find change tracking enabled for table: 'dbo.Products'.");
        }

        private void EnableChangeTrackingForDatabase()
        {
            this.ExecuteNonQuery($@"
                ALTER DATABASE [{this.DatabaseName}]
                SET CHANGE_TRACKING = ON
                (CHANGE_RETENTION = 2 DAYS, AUTO_CLEANUP = ON);
            ");
        }

        private void EnableChangeTrackingForTable(string tableName)
        {
            this.ExecuteNonQuery($@"
                ALTER TABLE [dbo].[{tableName}]
                ENABLE CHANGE_TRACKING;
            ");
        }

        private void MonitorProductChanges(List<SqlChange<Product>> changes, string messagePrefix)
        {
            int index = 0;

            this.FunctionHost.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null && (index = e.Data.IndexOf(messagePrefix, StringComparison.Ordinal)) >= 0)
                {
                    string json = e.Data[(index + messagePrefix.Length)..];
                    changes.AddRange(JsonConvert.DeserializeObject<IReadOnlyList<SqlChange<Product>>>(json));
                }
            };
        }

        private void InsertProducts(int first_id, int last_id)
        {
            int count = last_id - first_id + 1;
            this.ExecuteNonQuery(
                "INSERT INTO [dbo].[Products] VALUES\n" +
                string.Join(",\n", Enumerable.Range(first_id, count).Select(id => $"({id}, 'Product {id}', {id * 100})")) + ";");
        }

        private void UpdateProducts(int first_id, int last_id)
        {
            int count = last_id - first_id + 1;
            this.ExecuteNonQuery(
                "UPDATE [dbo].[Products]\n" +
                "SET Name = 'Updated ' + Name\n" +
                "WHERE ProductId IN (" + string.Join(", ", Enumerable.Range(first_id, count)) + ");");
        }

        private void DeleteProducts(int first_id, int last_id)
        {
            int count = last_id - first_id + 1;
            this.ExecuteNonQuery(
                "DELETE FROM [dbo].[Products]\n" +
                "WHERE ProductId IN (" + string.Join(", ", Enumerable.Range(first_id, count)) + ");");
        }

        private static void ValidateProductChanges(List<SqlChange<Product>> changes, int first_id, int last_id,
            SqlChangeOperation operation, Func<int, string> getName, Func<int, int> getCost)
        {
            int count = last_id - first_id + 1;
            Assert.Equal(count, changes.Count);

            // Since the table rows are changed with a single SQL statement, the changes are not guaranteed to arrive in
            // ProductID-order. Occasionally, we find the items in the second batch are passed to the user function in
            // reverse order, which is an expected behavior.
            IEnumerable<SqlChange<Product>> orderedChanges = changes.OrderBy(change => change.Item.ProductID);

            int id = first_id;
            foreach (SqlChange<Product> change in orderedChanges)
            {
                Assert.Equal(operation, change.Operation);
                Product product = change.Item;
                Assert.NotNull(product);
                Assert.Equal(id, product.ProductID);
                Assert.Equal(getName(id), product.Name);
                Assert.Equal(getCost(id), product.Cost);
                id += 1;
            }
        }

        /// <summary>
        /// Launches the functions runtime host, waits for it to encounter error while starting the SQL trigger listener,
        /// and asserts that the logged error message matches with the supplied error message.
        /// </summary>
        /// <param name="functionName">Name of the user function that should cause error in trigger listener</param>
        /// <param name="useTestFolder">Whether the functions host should be launched from test folder</param>
        /// <param name="expectedErrorMessage">Expected error message string</param>
        private void StartFunctionsHostAndWaitForError(string functionName, bool useTestFolder, string expectedErrorMessage)
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
            this.StartFunctionHost(functionName, Common.SupportedLanguages.CSharp, useTestFolder, OutputHandler);

            // The functions host generally logs the error message within a second after starting up.
            const int BufferTimeForErrorInSeconds = 15;
            bool isCompleted = tcs.Task.Wait(TimeSpan.FromSeconds(BufferTimeForErrorInSeconds));

            this.FunctionHost.OutputDataReceived -= OutputHandler;
            this.FunctionHost.Kill();

            Assert.True(isCompleted, "Functions host did not log failure to start SQL trigger listener within specified time.");
            Assert.Equal(expectedErrorMessage, errorMessage);
        }
    }
}