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

        [Fact]
        public async Task SingleOperationTriggerTest()
        {
            this.EnableChangeTrackingForTable("Products");
            this.StartFunctionHost(nameof(ProductsTrigger), Common.SupportedLanguages.CSharp);

            var changes = new List<SqlChange<Product>>();
            this.MonitorProductChanges(changes);

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


        [Fact]
        public async Task MultiOperationTriggerTest()
        {
            this.EnableChangeTrackingForTable("Products");
            this.StartFunctionHost(nameof(ProductsTrigger), Common.SupportedLanguages.CSharp);

            var changes = new List<SqlChange<Product>>();
            this.MonitorProductChanges(changes);

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

        [Fact]
        public async Task TableNotPresentTriggerTest()
        {
            string exceptionMessage = await this.AssertTriggerListenerError(nameof(TableNotPresentTrigger));
            Assert.Equal("Could not find table: 'dbo.TableNotPresent'.", exceptionMessage);
        }

        [Fact]
        public async Task PrimaryKeyNotCreatedTriggerTest()
        {
            string exceptionMessage = await this.AssertTriggerListenerError(nameof(PrimaryKeyNotPresentTrigger));
            Assert.Equal("Could not find primary key created in table: 'dbo.ProductsWithoutPrimaryKey'.", exceptionMessage);
        }

        [Fact]
        public async Task ReservedPrimaryKeyColumnNamesTriggerTest()
        {
            string exceptionMessage = await this.AssertTriggerListenerError(nameof(ReservedPrimaryKeyColumnNamesTrigger));

            Assert.Equal(
                "Found reserved column name(s): 'ChangeVersion', 'AttemptCount', 'LeaseExpirationTime' in table: 'dbo.ProductsWithReservedPrimaryKeyColumnNames'." +
                " Please rename them to be able to use trigger binding.",
                exceptionMessage);
        }

        [Fact]
        public async Task ChangeTrackingNotEnabledTriggerTest()
        {
            string exceptionMessage = await this.AssertTriggerListenerError(nameof(ProductsTrigger));
            Assert.Equal("Could not find change tracking enabled for table: 'dbo.Products'.", exceptionMessage);
        }

        [Fact]
        public async Task UnsupportedColumnTypesTriggerTest()
        {
            string exceptionMessage = await this.AssertTriggerListenerError(nameof(UnsupportedColumnTypesTrigger));

            Assert.Equal(
                "Found column(s) with unsupported type(s): 'Location' (type: geography), 'Geometry' (type: geometry), 'Organization' (type: hierarchyid)" +
                " in table: 'dbo.ProductsWithUnsupportedColumnTypes'.",
                exceptionMessage);
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

        private void MonitorProductChanges(List<SqlChange<Product>> changes)
        {
            int index = 0;
            string prefix = "SQL Changes: ";

            this.FunctionHost.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null && (index = e.Data.IndexOf(prefix, StringComparison.Ordinal)) >= 0)
                {
                    string json = e.Data[(index + prefix.Length)..];
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

            int id = first_id;
            foreach (SqlChange<Product> change in changes)
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

        private async Task<string> AssertTriggerListenerError(string functionName)
        {
            string errorMessage = null;
            var tcs = new TaskCompletionSource<bool>();

            void OutputHandler(object sender, DataReceivedEventArgs e)
            {
                if (errorMessage == null && e.Data?.Contains("Failed to start SQL trigger listener") == true)
                {
                    string exceptionPrefix = "Exception: System.InvalidOperationException: ";
                    int index = e.Data.IndexOf(exceptionPrefix, StringComparison.Ordinal);
                    Assert.NotEqual(-1, index);
                    errorMessage = e.Data[(index + exceptionPrefix.Length)..];
                    tcs.SetResult(true);
                }
            };

            this.StartFunctionHost(functionName, Common.SupportedLanguages.CSharp, false, OutputHandler);
            Task completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Equal(tcs.Task, completedTask);
            this.FunctionHost.OutputDataReceived -= OutputHandler;

            // WebJobs SDK retries forever to start the trigger-listener until it succeeds, which makes the Functions
            // Host process never exit by itself in case of an error.
            Assert.False(this.FunctionHost.HasExited);
            this.FunctionHost.Kill();

            Assert.NotNull(errorMessage);
            return errorMessage;
        }
    }
}