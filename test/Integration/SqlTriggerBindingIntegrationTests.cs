// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.TriggerBindingSamples;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlTriggerConstants;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using xRetry;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    [Collection(IntegrationTestsCollection.Name)]
    public class SqlTriggerBindingIntegrationTests : SqlTriggerBindingIntegrationTestBase
    {
        public SqlTriggerBindingIntegrationTests(ITestOutputHelper output = null) : base(output)
        {
        }

        /// <summary>
        /// Ensures that the user function gets invoked for each of the insert, update and delete operation.
        /// </summary>
        [RetryTheory]
        [SqlInlineData()]
        public async Task SingleOperationTriggerTest(SupportedLanguages lang)
        {
            this.SetChangeTrackingForTable("Products");
            this.StartFunctionHost(nameof(ProductsTrigger), lang);

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
        /// Verifies that manually setting the batch size using the original config var correctly changes the
        /// number of changes processed at once.
        /// </summary>
        [RetryTheory]
        [SqlInlineData()]
        public async Task BatchSizeOverrideTriggerTest(SupportedLanguages lang)
        {
            // Use enough items to require 4 batches to be processed but then
            // set the max batch size to the same value so they can all be processed in one
            // batch. The test will only wait for ~1 batch worth of time so will timeout
            // if the max batch size isn't actually changed
            const int maxBatchSize = SqlTableChangeMonitor<object>.DefaultMaxBatchSize * 4;
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
                lang,
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
        }

        /// <summary>
        /// Verifies that manually setting the max batch size correctly changes the number of changes processed at once
        /// </summary>
        [RetryTheory]
        [SqlInlineData()]
        public async Task MaxBatchSizeOverrideTriggerTest(SupportedLanguages lang)
        {
            // Use enough items to require 4 batches to be processed but then
            // set the max batch size to the same value so they can all be processed in one
            // batch. The test will only wait for ~1 batch worth of time so will timeout
            // if the max batch size isn't actually changed
            const int maxBatchSize = SqlTableChangeMonitor<object>.DefaultMaxBatchSize * 4;
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
                lang,
                useTestFolder: true,
                customOutputHandler: handler,
                environmentVariables: new Dictionary<string, string>() {
                    { "TEST_EXPECTED_MAX_BATCH_SIZE", maxBatchSize.ToString() },
                    { "Sql_Trigger_MaxBatchSize", maxBatchSize.ToString() }
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
        }

        /// <summary>
        /// Verifies that manually setting the polling interval correctly changes the delay between processing each batch of changes
        /// </summary>
        [RetryTheory]
        [SqlInlineData()]
        public async Task PollingIntervalOverrideTriggerTest(SupportedLanguages lang)
        {
            const int firstId = 1;
            // Use enough items to require 5 batches to be processed - the test will
            // only wait for the expected time and timeout if the default polling
            // interval isn't actually modified.
            const int lastId = SqlTableChangeMonitor<object>.DefaultMaxBatchSize * 5;
            const int pollingIntervalMs = SqlTableChangeMonitor<object>.DefaultPollingIntervalMs / 2;
            this.SetChangeTrackingForTable("Products");
            var taskCompletionSource = new TaskCompletionSource<bool>();
            DataReceivedEventHandler handler = TestUtils.CreateOutputReceievedHandler(
                taskCompletionSource,
                @"Starting change consumption loop. MaxBatchSize: \d* PollingIntervalMs: (\d*)",
                "PollingInterval",
                pollingIntervalMs.ToString());
            this.StartFunctionHost(
                nameof(ProductsTriggerWithValidation),
                lang,
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

        /// <summary>
        /// Verifies that if several changes have happened to the table row since last invocation, then a single net
        /// change for that row is passed to the user function.
        /// </summary>
        [RetryTheory]
        [SqlInlineData()]
        public async Task MultiOperationTriggerTest(SupportedLanguages lang)
        {
            int firstId = 1;
            int lastId = 5;
            this.SetChangeTrackingForTable("Products");
            this.StartFunctionHost(nameof(ProductsTrigger), lang);

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
        [RetryTheory]
        [SqlInlineData()]
        public async Task MultiFunctionTriggerTest(SupportedLanguages lang)
        {
            const string Trigger1Changes = "Trigger1 Changes: ";
            const string Trigger2Changes = "Trigger2 Changes: ";

            this.SetChangeTrackingForTable("Products");

            string functionList = $"{nameof(MultiFunctionTrigger.MultiFunctionTrigger1)} {nameof(MultiFunctionTrigger.MultiFunctionTrigger2)}";
            this.StartFunctionHost(functionList, lang, useTestFolder: true);

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
        [RetryTheory]
        [SqlInlineData()]
        public async Task MultiHostTriggerTest(SupportedLanguages lang)
        {
            this.SetChangeTrackingForTable("Products");

            // Prepare three function host processes.
            this.StartFunctionHost(nameof(ProductsTrigger), lang);
            this.StartFunctionHost(nameof(ProductsTrigger), lang);
            this.StartFunctionHost(nameof(ProductsTrigger), lang);

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
        [RetryTheory]
        [SqlInlineData()]
        public void TableNotPresentTriggerTest(SupportedLanguages lang)
        {
            this.StartFunctionHostAndWaitForError(
                nameof(TableNotPresentTrigger),
                lang,
                true,
                "Could not find table: 'dbo.TableNotPresent'.");
        }

        /// <summary>
        /// Tests the error message when the user table does not contain primary key.
        /// </summary>
        [RetryTheory]
        [SqlInlineData()]
        public void PrimaryKeyNotCreatedTriggerTest(SupportedLanguages lang)
        {
            this.StartFunctionHostAndWaitForError(
                nameof(PrimaryKeyNotPresentTrigger),
                lang,
                true,
                "Could not find primary key created in table: 'dbo.ProductsWithoutPrimaryKey'.");
        }

        /// <summary>
        /// Tests the error message when the user table contains one or more primary keys with names conflicting with
        /// column names in the leases table.
        /// </summary>
        [RetryTheory]
        [SqlInlineData()]
        public void ReservedPrimaryKeyColumnNamesTriggerTest(SupportedLanguages lang)
        {
            this.StartFunctionHostAndWaitForError(
                nameof(ReservedPrimaryKeyColumnNamesTrigger),
                lang,
                true,
                "Found reserved column name(s): '_az_func_ChangeVersion', '_az_func_AttemptCount', '_az_func_LeaseExpirationTime' in table: 'dbo.ProductsWithReservedPrimaryKeyColumnNames'." +
                " Please rename them to be able to use trigger binding.");
        }

        /// <summary>
        /// Tests the error message when the user table contains columns of unsupported SQL types.
        /// </summary>
        [RetryTheory]
        [SqlInlineData()]
        public void UnsupportedColumnTypesTriggerTest(SupportedLanguages lang)
        {
            this.StartFunctionHostAndWaitForError(
                nameof(UnsupportedColumnTypesTrigger),
                lang,
                true,
                "Found column(s) with unsupported type(s): 'Location' (type: geography), 'Geometry' (type: geometry), 'Organization' (type: hierarchyid)" +
                " in table: 'dbo.ProductsWithUnsupportedColumnTypes'.");
        }

        /// <summary>
        /// Tests the error message when change tracking is not enabled on the user table.
        /// </summary>
        [RetryTheory]
        [SqlInlineData()]
        public void ChangeTrackingNotEnabledTriggerTest(SupportedLanguages lang)
        {
            this.StartFunctionHostAndWaitForError(
                nameof(ProductsTrigger),
                lang,
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
            string userFunctionId = "func-id";
            IConfiguration configuration = new ConfigurationBuilder().Build();
            var listener = new SqlTriggerListener<Product>(this.DbConnectionString, "dbo.Products", userFunctionId, Mock.Of<ITriggeredFunctionExecutor>(), Mock.Of<ILogger>(), configuration);
            await listener.StartAsync(CancellationToken.None);
            // Cancel immediately so the listener doesn't start processing the changes
            await listener.StopAsync(CancellationToken.None);
            var metricsProvider = new SqlTriggerMetricsProvider(this.DbConnectionString, Mock.Of<ILogger>(), new SqlObject("dbo.Products"), userFunctionId);
            SqlTriggerMetrics metrics = await metricsProvider.GetMetricsAsync();
            Assert.True(metrics.UnprocessedChangeCount == 0, "There should initially be 0 unprocessed changes");
            this.InsertProducts(1, 5);
            metrics = await metricsProvider.GetMetricsAsync();
            Assert.True(metrics.UnprocessedChangeCount == 5, $"There should be 5 unprocessed changes after insertion. Actual={metrics.UnprocessedChangeCount}");
        }

        /// <summary>
        /// Tests that when using an unsupported database the expected error is thrown
        /// </summary>
        [RetryTheory]
        [SqlInlineData()]
        public void UnsupportedDatabaseThrows(SupportedLanguages lang)
        {
            // Change database compat level to unsupported version
            this.ExecuteNonQuery($"ALTER DATABASE {this.DatabaseName} SET COMPATIBILITY_LEVEL = 120");

            this.StartFunctionHostAndWaitForError(
                nameof(ProductsTrigger),
                lang,
                false,
                "SQL bindings require a database compatibility level of 130 or higher to function. Current compatibility level = 120");

            // Change database compat level back to supported level
            this.ExecuteNonQuery($"ALTER DATABASE {this.DatabaseName} SET COMPATIBILITY_LEVEL = 150");
        }

        /// <summary>
        /// Tests that when a user function throws an exception we'll retry executing that function once the lease timeout expires
        /// </summary>
        [RetryTheory]
        [SqlInlineData()]
        [UnsupportedLanguages(SupportedLanguages.JavaScript, SupportedLanguages.Python, SupportedLanguages.PowerShell, SupportedLanguages.Csx, SupportedLanguages.Java)] // Keeping static state for threwException across calls is only valid for C# and Java.
        public async Task FunctionExceptionsCauseRetry(SupportedLanguages lang)
        {
            this.SetChangeTrackingForTable("Products");
            this.StartFunctionHost(nameof(TriggerWithException), lang, useTestFolder: true);
            TaskCompletionSource taskCompletionSource = new();
            void TestExceptionMessageSeen(object sender, DataReceivedEventArgs e)
            {
                if (e.Data.Contains(TriggerWithException.ExceptionMessage))
                {
                    taskCompletionSource.SetResult();
                }
            };
            this.FunctionHost.OutputDataReceived += TestExceptionMessageSeen;
            int firstId = 1;
            int lastId = 30;
            int batchProcessingTimeout = this.GetBatchProcessingTimeout(1, 30);
            Task changesTask = this.WaitForProductChanges(
                firstId,
                lastId,
                SqlChangeOperation.Insert,
                () => { this.InsertProducts(firstId, lastId); return Task.CompletedTask; },
                id => $"Product {id}",
                id => id * 100,
                (SqlTableChangeMonitor<object>.LeaseIntervalInSeconds * 1000) + batchProcessingTimeout);

            // First wait for the exception message to show up
            await taskCompletionSource.Task.TimeoutAfter(TimeSpan.FromMilliseconds(batchProcessingTimeout), "Timed out waiting for exception message");
            // Now wait for the retry to occur and successfully pass
            await changesTask;

        }

        /// <summary>
        /// Tests that the GlobalState table has LastAccessTime column.
        /// </summary>
        /// <remarks>We call StartAsync which initializes the GlobalState and then drop the LastAccessTime column from the table and recall the StartAsync to check if the GlobalState has the column.</remarks>
        [Fact]
        public async void LastAccessTimeColumn_Created_OnStartup()
        {

            this.SetChangeTrackingForTable("Products");
            string userFunctionId = "func-id";
            IConfiguration configuration = new ConfigurationBuilder().Build();
            var listener = new SqlTriggerListener<Product>(this.DbConnectionString, "dbo.Products", userFunctionId, Mock.Of<ITriggeredFunctionExecutor>(), Mock.Of<ILogger>(), configuration);
            await listener.StartAsync(CancellationToken.None);
            // Cancel immediately so the listener doesn't start processing the changes
            await listener.StopAsync(CancellationToken.None);
            //Check if LastAccessTime column exists in the GlobalState table
            Assert.True(1 == (int)this.ExecuteScalar($@"SELECT 1 FROM sys.columns WHERE Name = N'{LastAccessTimeColumnName}' AND Object_ID = Object_ID(N'{GlobalStateTableName}')"), $"{GlobalStateTableName} should have {LastAccessTimeColumnName} column on creation");
            // Delete default constraint(s) on LastAccessTime column before dropping it
            string deleteDefaultContraint = $@"DECLARE @sql NVARCHAR(MAX)
                                            WHILE 1=1
                                            BEGIN
                                                SELECT TOP 1 @sql = N'ALTER TABLE {GlobalStateTableName} DROP CONSTRAINT ['+dc.NAME+N']'
                                                FROM sys.default_constraints dc
                                                JOIN sys.columns c
                                                    ON c.default_object_id = dc.object_id
                                                WHERE dc.parent_object_id = OBJECT_ID('{GlobalStateTableName}')
                                                AND c.name = N'{LastAccessTimeColumnName}'
                                                IF @@ROWCOUNT = 0 BREAK
                                                EXEC (@sql)
                                            END";
            this.ExecuteNonQuery(deleteDefaultContraint);
            // Delete the LastAccessTime column from GlobalState table.
            this.ExecuteNonQuery($"ALTER TABLE {GlobalStateTableName} DROP COLUMN {LastAccessTimeColumnName}");

            await listener.StartAsync(CancellationToken.None);
            // Cancel immediately so the listener doesn't start processing the changes
            await listener.StopAsync(CancellationToken.None);

            //Check if LastAccessTime column exists in the GlobalState table
            Assert.True(1 == (int)this.ExecuteScalar("SELECT 1 FROM sys.columns WHERE Name = N'LastAccessTime' AND Object_ID = Object_ID(N'[az_func].[GlobalState]')"), $"{GlobalStateTableName} should have {LastAccessTimeColumnName} column after restarting the listener.");
        }

        /// <summary>
        /// Tests that trigger function executes on table whose name is a reserved word (User).
        /// </summary>
        [Theory]
        [SqlInlineData()]
        public async void ReservedTableNameTriggerTest(SupportedLanguages lang)
        {
            this.SetChangeTrackingForTable("User");
            this.StartFunctionHost(nameof(ReservedTableNameTrigger), lang, true);
            User expectedResponse = Utils.JsonDeserializeObject<User>(/*lang=json,strict*/ "{\"UserId\":999,\"UserName\":\"test\",\"FullName\":\"Testy Test\"}");
            int index = 0;
            string messagePrefix = "SQL Changes: ";

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
                    IReadOnlyList<SqlChange<User>> changes;
                    try
                    {
                        changes = Utils.JsonDeserializeObject<IReadOnlyList<SqlChange<User>>>(json);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Exception deserializing JSON content. Error={ex.Message} Json=\"{json}\"", ex);
                    }
                    Assert.Equal(SqlChangeOperation.Insert, changes[0].Operation); // Expected change operation
                    User user = changes[0].Item;
                    Assert.NotNull(user); // user deserialized correctly
                    Assert.Equal(expectedResponse, user); // user has the expected values
                    taskCompletion.SetResult(true);
                }
            };

            // Set up listener for the changes coming in
            foreach (Process functionHost in this.FunctionHostList)
            {
                functionHost.OutputDataReceived += MonitorOutputData;
            }

            // Now that we've set up our listener trigger the actions to monitor
            this.ExecuteNonQuery("INSERT INTO [dbo].[User] VALUES (" +
                "999, " + // UserId,
                "'test', " + // UserName
                "'Testy Test')"); // FullName

            // Now wait until either we timeout or we've gotten all the expected changes, whichever comes first
            this.LogOutput($"[{DateTime.UtcNow:u}] Waiting for Insert changes (10000ms)");
            await taskCompletion.Task.TimeoutAfter(TimeSpan.FromMilliseconds(10000), $"Timed out waiting for Insert changes.");

            // Unhook handler since we're done monitoring these changes so we aren't checking other changes done later
            foreach (Process functionHost in this.FunctionHostList)
            {
                functionHost.OutputDataReceived -= MonitorOutputData;
            }
        }

        /// <summary>
        /// Ensures that all column types are serialized correctly.
        /// </summary>
        [Theory]
        [SqlInlineData()]
        public async Task ProductsColumnTypesTriggerTest(SupportedLanguages lang)
        {
            this.SetChangeTrackingForTable("ProductsColumnTypes");
            this.StartFunctionHost(nameof(ProductsColumnTypesTrigger), lang, true);
            ProductColumnTypes expectedResponse = Utils.JsonDeserializeObject<ProductColumnTypes>(/*lang=json,strict*/ "{\"ProductId\":999,\"BigInt\":999,\"Bit\":true,\"DecimalType\":1.2345,\"Money\":1.2345,\"Numeric\":1.2345,\"SmallInt\":1,\"SmallMoney\":1.2345,\"TinyInt\":1,\"FloatType\":0.1,\"Real\":0.1,\"Date\":\"2022-10-20T00:00:00.000Z\",\"Datetime\":\"2022-10-20T12:39:13.123Z\",\"Datetime2\":\"2022-10-20T12:39:13.123Z\",\"DatetimeOffset\":\"2022-10-20T12:39:13.123Z\",\"SmallDatetime\":\"2022-10-20T12:39:00.000Z\",\"Time\":\"12:39:13.1230000\",\"CharType\":\"test\",\"Varchar\":\"test\",\"Nchar\":\"test\",\"Nvarchar\":\"test\",\"Binary\":\"dGVzdA==\",\"Varbinary\":\"dGVzdA==\"}");
            int index = 0;
            string messagePrefix = "SQL Changes: ";

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
                    IReadOnlyList<SqlChange<ProductColumnTypes>> changes;
                    try
                    {
                        changes = Utils.JsonDeserializeObject<IReadOnlyList<SqlChange<ProductColumnTypes>>>(json);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Exception deserializing JSON content. Error={ex.Message} Json=\"{json}\"", ex);
                    }
                    Assert.Equal(SqlChangeOperation.Insert, changes[0].Operation); // Expected change operation
                    ProductColumnTypes product = changes[0].Item;
                    Assert.NotNull(product); // Product deserialized correctly
                    Assert.Equal(expectedResponse, product); // The product has the expected values
                    taskCompletion.SetResult(true);
                }
            };

            // Set up listener for the changes coming in
            foreach (Process functionHost in this.FunctionHostList)
            {
                functionHost.OutputDataReceived += MonitorOutputData;
            }

            // Now that we've set up our listener trigger the actions to monitor
            string datetime = "2022-10-20 12:39:13.123";
            this.ExecuteNonQuery("INSERT INTO [dbo].[ProductsColumnTypes] VALUES (" +
                "999, " + // ProductId,
                "999, " + // BigInt
                "1, " + // Bit
                "1.2345, " + // DecimalType
                "1.2345, " + // Money
                "1.2345, " + // Numeric
                "1, " + // SmallInt
                "1.2345, " + // SmallMoney
                "1, " + // TinyInt
                ".1, " + // FloatType
                ".1, " + // Real
                $"CONVERT(DATE, '{datetime}'), " + // Date
                $"CONVERT(DATETIME, '{datetime}'), " + // Datetime
                $"CONVERT(DATETIME2, '{datetime}'), " + // Datetime2
                $"CONVERT(DATETIMEOFFSET, '{datetime}'), " + // DatetimeOffset
                $"CONVERT(SMALLDATETIME, '{datetime}'), " + // SmallDatetime
                $"CONVERT(TIME, '{datetime}'), " + // Time
                "'test', " + // CharType
                "'test', " + // Varchar
                "'test', " + // Nchar
                "'test', " +  // Nvarchar
                "CONVERT(BINARY, 'test'), " + // Binary
                "CONVERT(VARBINARY, 'test'))"); // Varbinary

            // Now wait until either we timeout or we've gotten all the expected changes, whichever comes first
            this.LogOutput($"[{DateTime.UtcNow:u}] Waiting for Insert changes (10000ms)");
            await taskCompletion.Task.TimeoutAfter(TimeSpan.FromMilliseconds(10000), $"Timed out waiting for Insert changes.");

            // Unhook handler since we're done monitoring these changes so we aren't checking other changes done later
            foreach (Process functionHost in this.FunctionHostList)
            {
                functionHost.OutputDataReceived -= MonitorOutputData;
            }
        }
    }
}