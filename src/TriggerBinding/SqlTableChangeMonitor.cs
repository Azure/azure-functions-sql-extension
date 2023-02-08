// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry;
using static Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry.Telemetry;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlTriggerConstants;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Watches for changes in the user table, invokes user function if changes are found, and manages leases.
    /// </summary>
    /// <typeparam name="T">POCO class representing the row in the user table</typeparam>
    internal sealed class SqlTableChangeMonitor<T> : IDisposable
    {
        #region Constants
        /// <summary>
        /// The maximum number of times we'll attempt to process a change before giving up
        /// </summary>
        private const int MaxChangeProcessAttemptCount = 5;
        /// <summary>
        /// The maximum number of times that we'll attempt to renew a lease be
        /// </summary>
        /// <remarks>
        /// Leases are held for approximately (LeaseRenewalIntervalInSeconds * MaxLeaseRenewalCount) seconds. It is
        // required to have at least one of (LeaseIntervalInSeconds / LeaseRenewalIntervalInSeconds) attempts to
        // renew the lease succeed to prevent it from expiring.
        // </remarks>
        private const int MaxLeaseRenewalCount = 10;
        public const int LeaseIntervalInSeconds = 60;
        private const int LeaseRenewalIntervalInSeconds = 15;
        private const int MaxRetryReleaseLeases = 3;

        public const int DefaultMaxBatchSize = 100;
        public const int DefaultPollingIntervalMs = 1000;
        #endregion Constants

        private readonly string _connectionString;
        private readonly int _userTableId;
        private readonly SqlObject _userTable;
        private readonly string _userFunctionId;
        private readonly string _leasesTableName;
        private readonly IReadOnlyList<string> _userTableColumns;
        private readonly IReadOnlyList<(string name, string type)> _primaryKeyColumns;
        private readonly IReadOnlyList<string> _rowMatchConditions;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ILogger _logger;
        /// <summary>
        /// Maximum number of changes to process in each iteration of the loop
        /// </summary>
        private readonly int _maxBatchSize = DefaultMaxBatchSize;
        /// <summary>
        /// Delay in ms between processing each batch of changes
        /// </summary>
        private readonly int _pollingIntervalInMs = DefaultPollingIntervalMs;

        private readonly CancellationTokenSource _cancellationTokenSourceCheckForChanges = new CancellationTokenSource();
        private readonly CancellationTokenSource _cancellationTokenSourceRenewLeases = new CancellationTokenSource();
        private CancellationTokenSource _cancellationTokenSourceExecutor = new CancellationTokenSource();

        // The semaphore gets used by lease-renewal loop to ensure that '_state' stays set to 'ProcessingChanges' while
        // the leases are being renewed. The change-consumption loop requires to wait for the semaphore before modifying
        // the value of '_state' back to 'CheckingForChanges'. Since the field '_rows' is only updated if the value of
        // '_state' is set to 'CheckingForChanges', this guarantees that '_rows' will stay same while it is being
        // iterated over inside the lease-renewal loop.
        private readonly SemaphoreSlim _rowsLock = new SemaphoreSlim(1, 1);

        private readonly IDictionary<TelemetryPropertyName, string> _telemetryProps;

        /// <summary>
        /// Rows that are currently being processed
        /// </summary>
        private IReadOnlyList<IReadOnlyDictionary<string, object>> _rowsToProcess = new List<IReadOnlyDictionary<string, object>>();
        /// <summary>
        /// Rows that have been processed and now need to have their leases released
        /// </summary>
        private IReadOnlyList<IReadOnlyDictionary<string, object>> _rowsToRelease = new List<IReadOnlyDictionary<string, object>>();
        private int _leaseRenewalCount = 0;
        private State _state = State.CheckingForChanges;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTableChangeMonitor{T}" />> class.
        /// </summary>
        /// <param name="connectionString">SQL connection string used to connect to user database</param>
        /// <param name="userTableId">SQL object ID of the user table</param>
        /// <param name="userTable"><see cref="SqlObject"> instance created with user table name</param>
        /// <param name="userFunctionId">Unique identifier for the user function</param>
        /// <param name="leasesTableName">Name of the leases table</param>
        /// <param name="userTableColumns">List of all column names in the user table</param>
        /// <param name="primaryKeyColumns">List of primary key column names in the user table</param>
        /// <param name="executor">Defines contract for triggering user function</param>
        /// <param name="logger">Facilitates logging of messages</param>
        /// <param name="configuration">Provides configuration values</param>
        /// <param name="telemetryProps">Properties passed in telemetry events</param>
        public SqlTableChangeMonitor(
            string connectionString,
            int userTableId,
            SqlObject userTable,
            string userFunctionId,
            string leasesTableName,
            IReadOnlyList<string> userTableColumns,
            IReadOnlyList<(string name, string type)> primaryKeyColumns,
            ITriggeredFunctionExecutor executor,
            ILogger logger,
            IConfiguration configuration,
            IDictionary<TelemetryPropertyName, string> telemetryProps)
        {
            this._connectionString = !string.IsNullOrEmpty(connectionString) ? connectionString : throw new ArgumentNullException(nameof(connectionString));
            this._userTable = !string.IsNullOrEmpty(userTable?.FullName) ? userTable : throw new ArgumentNullException(nameof(userTable));
            this._userFunctionId = !string.IsNullOrEmpty(userFunctionId) ? userFunctionId : throw new ArgumentNullException(nameof(userFunctionId));
            this._leasesTableName = !string.IsNullOrEmpty(leasesTableName) ? leasesTableName : throw new ArgumentNullException(nameof(leasesTableName));
            this._userTableColumns = userTableColumns ?? throw new ArgumentNullException(nameof(userTableColumns));
            this._primaryKeyColumns = primaryKeyColumns ?? throw new ArgumentNullException(nameof(primaryKeyColumns));
            this._executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));

            this._userTableId = userTableId;
            this._telemetryProps = telemetryProps ?? new Dictionary<TelemetryPropertyName, string>();

            // Check if there's config settings to override the default max batch size/polling interval values
            int? configuredMaxBatchSize = configuration.GetValue<int?>(ConfigKey_SqlTrigger_MaxBatchSize);
            // Fall back to original value for backwards compat if the new value isn't specified
            if (configuredMaxBatchSize == null)
            {
                configuredMaxBatchSize = configuration.GetValue<int?>(ConfigKey_SqlTrigger_BatchSize);
            }
            int? configuredPollingInterval = configuration.GetValue<int?>(ConfigKey_SqlTrigger_PollingInterval);
            this._maxBatchSize = configuredMaxBatchSize ?? this._maxBatchSize;
            if (this._maxBatchSize <= 0)
            {
                throw new InvalidOperationException($"Invalid value for configuration setting '{ConfigKey_SqlTrigger_MaxBatchSize}'. Ensure that the value is a positive integer.");
            }
            this._pollingIntervalInMs = configuredPollingInterval ?? this._pollingIntervalInMs;
            if (this._pollingIntervalInMs <= 0)
            {
                throw new InvalidOperationException($"Invalid value for configuration setting '{ConfigKey_SqlTrigger_PollingInterval}'. Ensure that the value is a positive integer.");
            }
            TelemetryInstance.TrackEvent(
                TelemetryEventName.TriggerMonitorStart,
                new Dictionary<TelemetryPropertyName, string>(telemetryProps) {
                        { TelemetryPropertyName.HasConfiguredMaxBatchSize, (configuredMaxBatchSize != null).ToString() },
                        { TelemetryPropertyName.HasConfiguredPollingInterval, (configuredPollingInterval != null).ToString() },
                },
                new Dictionary<TelemetryMeasureName, double>() {
                    { TelemetryMeasureName.MaxBatchSize, this._maxBatchSize },
                    { TelemetryMeasureName.PollingIntervalMs, this._pollingIntervalInMs }
                }
            );

            // Prep search-conditions that will be used besides WHERE clause to match table rows.
            this._rowMatchConditions = Enumerable.Range(0, this._maxBatchSize)
                .Select(rowIndex => string.Join(" AND ", this._primaryKeyColumns.Select((col, colIndex) => $"{col.name.AsBracketQuotedString()} = @{rowIndex}_{colIndex}")))
                .ToList();

#pragma warning disable CS4014 // Queue the below tasks and exit. Do not wait for their completion.
            _ = Task.Run(() =>
            {
                this.RunChangeConsumptionLoopAsync();
                this.RunLeaseRenewalLoopAsync();
            });
#pragma warning restore CS4014
        }

        public void Dispose()
        {
            // When the CheckForChanges loop is finished, it will cancel the lease renewal loop.
            this._cancellationTokenSourceCheckForChanges.Cancel();
        }

        public async Task<long> GetUnprocessedChangeCountAsync()
        {
            long unprocessedChangeCount = 0L;

            try
            {
                long getUnprocessedChangesDurationMs = 0L;

                using (var connection = new SqlConnection(this._connectionString))
                {
                    this._logger.LogDebugWithThreadId("BEGIN OpenGetUnprocessedChangesConnection");
                    await connection.OpenAsync();
                    this._logger.LogDebugWithThreadId("END OpenGetUnprocessedChangesConnection");

                    // Use a transaction to automatically release the app lock when we're done executing the query
                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead))
                    {
                        try
                        {
                            using (SqlCommand getUnprocessedChangesCommand = this.BuildGetUnprocessedChangesCommand(connection, transaction))
                            {
                                this._logger.LogInformation("Getting change count");
                                this._logger.LogDebugWithThreadId($"BEGIN GetUnprocessedChangeCount Query={getUnprocessedChangesCommand.CommandText}");
                                var commandSw = Stopwatch.StartNew();
                                unprocessedChangeCount = (long)await getUnprocessedChangesCommand.ExecuteScalarAsync();
                                getUnprocessedChangesDurationMs = commandSw.ElapsedMilliseconds;
                            }

                            this._logger.LogDebugWithThreadId($"END GetUnprocessedChangeCount Duration={getUnprocessedChangesDurationMs}ms Count={unprocessedChangeCount}");
                            transaction.Commit();
                        }
                        catch (Exception)
                        {
                            try
                            {
                                transaction.Rollback();
                            }
                            catch (Exception ex2)
                            {
                                this._logger.LogError($"GetUnprocessedChangeCount : Failed to rollback transaction due to exception: {ex2.GetType()}. Exception message: {ex2.Message}");
                                TelemetryInstance.TrackException(TelemetryErrorName.GetUnprocessedChangeCountRollback, ex2, this._telemetryProps);
                            }
                            throw;
                        }
                    }
                }

                var measures = new Dictionary<TelemetryMeasureName, double>
                {
                    [TelemetryMeasureName.GetUnprocessedChangesDurationMs] = getUnprocessedChangesDurationMs,
                    [TelemetryMeasureName.UnprocessedChangeCount] = unprocessedChangeCount,
                };
            }
            catch (Exception ex)
            {
                this._logger.LogError($"Failed to query count of unprocessed changes for table '{this._userTable.FullName}' due to exception: {ex.GetType()}. Exception message: {ex.Message}");
                TelemetryInstance.TrackException(TelemetryErrorName.GetUnprocessedChangeCount, ex, this._telemetryProps);
                throw;
            }

            return unprocessedChangeCount;
        }

        /// <summary>
        /// Executed once every <see cref="_pollingIntervalInMs"/> period. Each iteration will go through a series of state
        /// changes, if an error occurs in any of them then it will skip the rest of the states and try again in the next
        /// iteration.
        /// It starts in the <see cref="State.CheckingForChanges"/> check and queries the change/leases tables for changes on the
        /// user's table.
        /// If any are found, the state is transitioned to <see cref="State.ProcessingChanges"/> and the user's
        /// function is executed with the found changes.
        /// Finally the state moves to <see cref="State.Cleanup" /> and if the execution was successful,
        /// the leases on "_rows" are released. Finally the state transitions to <see cref="State.CheckingForChanges"/>
        /// once again and starts over at the next iteration.
        /// </summary>
        private async Task RunChangeConsumptionLoopAsync()
        {
            this._logger.LogInformationWithThreadId($"Starting change consumption loop. MaxBatchSize: {this._maxBatchSize} PollingIntervalMs: {this._pollingIntervalInMs}");

            try
            {
                CancellationToken token = this._cancellationTokenSourceCheckForChanges.Token;

                using (var connection = new SqlConnection(this._connectionString))
                {
                    this._logger.LogDebugWithThreadId("BEGIN OpenChangeConsumptionConnection");
                    await connection.OpenAsync(token);
                    this._logger.LogDebugWithThreadId("END OpenChangeConsumptionConnection");

                    bool forceReconnect = false;
                    // Check for cancellation request only after a cycle of checking and processing of changes completes.
                    while (!token.IsCancellationRequested)
                    {
                        bool isConnected = await connection.TryEnsureConnected(forceReconnect, this._logger, "ChangeConsumptionConnection", token);
                        if (!isConnected)
                        {
                            // If we couldn't reconnect then wait our delay and try again
                            await Task.Delay(TimeSpan.FromMilliseconds(this._pollingIntervalInMs), token);
                            continue;
                        }
                        else
                        {
                            forceReconnect = false;
                        }
                        this._logger.LogDebugWithThreadId($"BEGIN ProcessingChanges State={this._state}");

                        try
                        {
                            // Process states sequentially since we normally expect the state to transition at the end
                            // of each previous state - but if an unexpected error occurs we'll skip the rest and then
                            // retry that state after the delay
                            if (this._state == State.CheckingForChanges)
                            {
                                await this.GetTableChangesAsync(connection, token);
                            }
                            if (this._state == State.ProcessingChanges)
                            {
                                await this.ProcessTableChangesAsync();
                            }
                            if (this._state == State.Cleanup)
                            {
                                await this.ReleaseLeasesAsync(connection, token);
                            }
                        }
                        catch (Exception e) when (e.IsFatalSqlException())
                        {
                            this._logger.LogError($"Fatal SQL Client exception processing changes. Will attempt to reestablish connection in {this._pollingIntervalInMs}ms. Exception = {e.Message}");
                            forceReconnect = true;
                        }
                        this._logger.LogDebugWithThreadId("END ProcessingChanges");
                        this._logger.LogDebugWithThreadId($"Delaying for {this._pollingIntervalInMs}ms");
                        await Task.Delay(TimeSpan.FromMilliseconds(this._pollingIntervalInMs), token);
                    }
                }
            }
            catch (Exception e)
            {
                // Only want to log the exception if it wasn't caused by StopAsync being called, since Task.Delay
                // throws an exception if it's cancelled.
                if (e.GetType() != typeof(TaskCanceledException))
                {
                    this._logger.LogError($"Exiting change consumption loop due to exception: {e.GetType()}. Exception message: {e.Message}");
                    TelemetryInstance.TrackException(TelemetryErrorName.ConsumeChangesLoop, e, this._telemetryProps);
                }
                throw;
            }
            finally
            {
                // If this thread exits due to any reason, then the lease renewal thread should exit as well. Otherwise,
                // it will keep looping perpetually.
                this._cancellationTokenSourceRenewLeases.Cancel();
                this._cancellationTokenSourceCheckForChanges.Dispose();
                this._cancellationTokenSourceExecutor.Dispose();
            }
        }

        /// <summary>
        /// Queries the change/leases tables to check for new changes on the user's table. If any are found, stores the
        /// change along with the corresponding data from the user table in "_rows".
        /// </summary>
        private async Task GetTableChangesAsync(SqlConnection connection, CancellationToken token)
        {
            this._logger.LogDebugWithThreadId("BEGIN GetTableChanges");
            try
            {
                var transactionSw = Stopwatch.StartNew();
                long setLastSyncVersionDurationMs = 0L, getChangesDurationMs = 0L, acquireLeasesDurationMs = 0L;

                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead))
                {
                    try
                    {
                        // Update the version number stored in the global state table if necessary before using it.
                        using (SqlCommand updateTablesPreInvocationCommand = this.BuildUpdateTablesPreInvocation(connection, transaction))
                        {
                            this._logger.LogDebugWithThreadId($"BEGIN UpdateTablesPreInvocation Query={updateTablesPreInvocationCommand.CommandText}");
                            var commandSw = Stopwatch.StartNew();
                            await updateTablesPreInvocationCommand.ExecuteNonQueryAsync(token);
                            setLastSyncVersionDurationMs = commandSw.ElapsedMilliseconds;
                        }
                        this._logger.LogDebugWithThreadId($"END UpdateTablesPreInvocation Duration={setLastSyncVersionDurationMs}ms");

                        var rows = new List<IReadOnlyDictionary<string, object>>();

                        // Use the version number to query for new changes.
                        using (SqlCommand getChangesCommand = this.BuildGetChangesCommand(connection, transaction))
                        {
                            this._logger.LogDebugWithThreadId($"BEGIN GetChanges Query={getChangesCommand.CommandText}");
                            var commandSw = Stopwatch.StartNew();

                            using (SqlDataReader reader = await getChangesCommand.ExecuteReaderAsync(token))
                            {
                                while (await reader.ReadAsync(token))
                                {
                                    rows.Add(SqlBindingUtilities.BuildDictionaryFromSqlRow(reader));
                                }
                            }

                            getChangesDurationMs = commandSw.ElapsedMilliseconds;
                        }
                        this._logger.LogDebugWithThreadId($"END GetChanges Duration={getChangesDurationMs}ms ChangedRows={rows.Count}");

                        // If changes were found, acquire leases on them.
                        if (rows.Count > 0)
                        {
                            using (SqlCommand acquireLeasesCommand = this.BuildAcquireLeasesCommand(connection, transaction, rows))
                            {
                                this._logger.LogDebugWithThreadId($"BEGIN AcquireLeases Query={acquireLeasesCommand.CommandText}");
                                var commandSw = Stopwatch.StartNew();
                                await acquireLeasesCommand.ExecuteNonQueryAsync(token);
                                acquireLeasesDurationMs = commandSw.ElapsedMilliseconds;
                            }
                            this._logger.LogDebugWithThreadId($"END AcquireLeases Duration={acquireLeasesDurationMs}ms");

                            // Only send event if we got changes to reduce the overall number of events sent since we generally
                            // only care about the times that we had to actually retrieve and process rows
                            var measures = new Dictionary<TelemetryMeasureName, double>
                            {
                                [TelemetryMeasureName.SetLastSyncVersionDurationMs] = setLastSyncVersionDurationMs,
                                [TelemetryMeasureName.GetChangesDurationMs] = getChangesDurationMs,
                                [TelemetryMeasureName.AcquireLeasesDurationMs] = acquireLeasesDurationMs,
                                [TelemetryMeasureName.TransactionDurationMs] = transactionSw.ElapsedMilliseconds,
                                [TelemetryMeasureName.BatchCount] = this._rowsToProcess.Count,
                            };
                            TelemetryInstance.TrackEvent(TelemetryEventName.GetChanges, this._telemetryProps, measures);
                        }

                        transaction.Commit();

                        // Set the rows for processing, now since the leases are acquired.
                        this._rowsToProcess = rows;
                        this._state = State.ProcessingChanges;
                    }
                    catch (Exception)
                    {
                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception ex)
                        {
                            this._logger.LogError($"Failed to rollback transaction due to exception: {ex.GetType()}. Exception message: {ex.Message}");
                            TelemetryInstance.TrackException(TelemetryErrorName.GetChangesRollback, ex, this._telemetryProps);
                        }
                        throw;
                    }
                }
            }
            catch (Exception e)
            {
                // If there's an exception in any part of the process, we want to clear all of our data in memory and
                // retry checking for changes again.
                this._rowsToProcess = new List<IReadOnlyDictionary<string, object>>();
                this._logger.LogError($"Failed to check for changes in table '{this._userTable.FullName}' due to exception: {e.GetType()}. Exception message: {e.Message}");
                TelemetryInstance.TrackException(TelemetryErrorName.GetChanges, e, this._telemetryProps);
                if (e.IsFatalSqlException())
                {
                    // If we get a fatal SQL Client exception here let it bubble up so we can try to re-establish the connection
                    throw;
                }
            }
            this._logger.LogDebugWithThreadId("END GetTableChanges");
        }

        private async Task ProcessTableChangesAsync()
        {
            this._logger.LogDebugWithThreadId("BEGIN ProcessTableChanges");
            if (this._rowsToProcess.Count > 0)
            {
                IReadOnlyList<SqlChange<T>> changes = null;

                try
                {
                    changes = this.ProcessChanges();
                }
                catch (Exception e)
                {
                    // Either there's a bug or we're in a bad state so not much we can do here. We'll try clearing
                    //  our state and retry getting the changes from the top again in case something broke while
                    // fetching the changes.
                    // It doesn't make sense to retry processing the changes immediately since this isn't a connection-based issue.
                    // We could probably send up the changes we were able to process and just skip the ones we couldn't, but given
                    // that this is not a case we expect would happen during normal execution we'll err on the side of caution for
                    // now and just retry getting the whole set of changes.
                    this._logger.LogError($"Failed to compose trigger parameter value for table: '{this._userTable.FullName} due to exception: {e.GetType()}. Exception message: {e.Message}");
                    TelemetryInstance.TrackException(TelemetryErrorName.ProcessChanges, e, this._telemetryProps);
                    await this.ClearRowsAsync();
                }

                if (changes != null)
                {
                    var input = new TriggeredFunctionData() { TriggerValue = changes };

                    this._logger.LogDebugWithThreadId("Executing triggered function");
                    var stopwatch = Stopwatch.StartNew();

                    FunctionResult result = await this._executor.TryExecuteAsync(input, this._cancellationTokenSourceExecutor.Token);
                    long durationMs = stopwatch.ElapsedMilliseconds;
                    var measures = new Dictionary<TelemetryMeasureName, double>
                    {
                        [TelemetryMeasureName.DurationMs] = durationMs,
                        [TelemetryMeasureName.BatchCount] = this._rowsToProcess.Count,
                    };
                    if (result.Succeeded)
                    {
                        this._logger.LogDebugWithThreadId($"Successfully triggered function. Duration={durationMs}ms");
                        TelemetryInstance.TrackEvent(TelemetryEventName.TriggerFunction, this._telemetryProps, measures);
                        // We've successfully fully processed these so set them to be released in the cleanup phase
                        this._rowsToRelease = this._rowsToProcess;
                        this._rowsToProcess = new List<IReadOnlyDictionary<string, object>>();
                    }
                    else
                    {
                        // In the future might make sense to retry executing the function, but for now we just let
                        // another worker try.
                        this._logger.LogError($"Failed to trigger user function for table: '{this._userTable.FullName} due to exception: {result.Exception.GetType()}. Exception message: {result.Exception.Message}");
                        TelemetryInstance.TrackException(TelemetryErrorName.TriggerFunction, result.Exception, this._telemetryProps, measures);
                    }
                    this._state = State.Cleanup;
                }
            }
            else
            {
                // This ideally should never happen, but as a safety measure ensure that if we tried to process changes but there weren't
                // any we still ensure everything is reset to a clean state
                await this.ClearRowsAsync();
            }
            this._logger.LogDebugWithThreadId("END ProcessTableChanges");
        }

        /// <summary>
        /// Executed once every <see cref="LeaseTime"/> period. If the state of the change monitor is
        /// <see cref="State.ProcessingChanges"/>, then we will renew the leases held by the change monitor on "_rows".
        /// </summary>
        private async void RunLeaseRenewalLoopAsync()
        {
            this._logger.LogInformation("Starting lease renewal loop.");

            try
            {
                CancellationToken token = this._cancellationTokenSourceRenewLeases.Token;

                using (var connection = new SqlConnection(this._connectionString))
                {
                    this._logger.LogDebugWithThreadId("BEGIN OpenLeaseRenewalLoopConnection");
                    await connection.OpenAsync(token);
                    this._logger.LogDebugWithThreadId("END OpenLeaseRenewalLoopConnection");

                    bool forceReconnect = false;
                    while (!token.IsCancellationRequested)
                    {
                        bool isConnected = await connection.TryEnsureConnected(forceReconnect, this._logger, "LeaseRenewalLoopConnection", token);
                        if (!isConnected)
                        {
                            // If we couldn't reconnect then wait our delay and try again
                            await Task.Delay(TimeSpan.FromSeconds(LeaseRenewalIntervalInSeconds), token);
                            continue;
                        }
                        else
                        {
                            forceReconnect = false;
                        }
                        try
                        {
                            await this.RenewLeasesAsync(connection, token);
                        }
                        catch (Exception e) when (e.IsFatalSqlException())
                        {
                            forceReconnect = true;
                        }

                        await Task.Delay(TimeSpan.FromSeconds(LeaseRenewalIntervalInSeconds), token);
                    }
                }
            }
            catch (Exception e)
            {
                // Only want to log the exception if it wasn't caused by StopAsync being called, since Task.Delay throws
                // an exception if it's cancelled.
                if (e.GetType() != typeof(TaskCanceledException))
                {
                    this._logger.LogError($"Exiting lease renewal loop due to exception: {e.GetType()}. Exception message: {e.Message}");
                    TelemetryInstance.TrackException(TelemetryErrorName.RenewLeasesLoop, e);
                }
            }
            finally
            {
                this._cancellationTokenSourceRenewLeases.Dispose();
            }
        }

        private async Task RenewLeasesAsync(SqlConnection connection, CancellationToken token)
        {
            this._logger.LogDebugWithThreadId("BEGIN WaitRowsLock - RenewLeases");
            await this._rowsLock.WaitAsync(token);
            this._logger.LogDebugWithThreadId("END WaitRowsLock - RenewLeases");

            if (this._state == State.ProcessingChanges && this._rowsToProcess.Count > 0)
            {
                // Use a transaction to automatically release the app lock when we're done executing the query
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead))
                {
                    try
                    {
                        using (SqlCommand renewLeasesCommand = this.BuildRenewLeasesCommand(connection, transaction))
                        {
                            this._logger.LogDebugWithThreadId($"BEGIN RenewLeases Query={renewLeasesCommand.CommandText}");
                            var stopwatch = Stopwatch.StartNew();

                            int rowsAffected = await renewLeasesCommand.ExecuteNonQueryAsync(token);

                            long durationMs = stopwatch.ElapsedMilliseconds;
                            this._logger.LogDebugWithThreadId($"END RenewLeases Duration={durationMs}ms RowsAffected={rowsAffected}");

                            if (rowsAffected > 0)
                            {
                                // Only send an event if we actually updated rows to reduce the overall number of events we send
                                var measures = new Dictionary<TelemetryMeasureName, double>
                                {
                                    [TelemetryMeasureName.DurationMs] = durationMs,
                                };

                                TelemetryInstance.TrackEvent(TelemetryEventName.RenewLeases, this._telemetryProps, measures);
                            }


                            transaction.Commit();
                        }
                    }
                    catch (Exception e)
                    {
                        // This catch block is necessary so that the finally block is executed even in the case of an exception
                        // (see https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/try-finally, third
                        // paragraph). If we fail to renew the leases, multiple workers could be processing the same change
                        // data, but we have functionality in place to deal with this (see design doc).
                        this._logger.LogError($"Failed to renew leases due to exception: {e.GetType()}. Exception message: {e.Message}");
                        TelemetryInstance.TrackException(TelemetryErrorName.RenewLeases, e, this._telemetryProps);

                        try
                        {
                            transaction.Rollback();
                        }
                        catch (Exception e2)
                        {
                            this._logger.LogError($"RenewLeases - Failed to rollback transaction due to exception: {e2.GetType()}. Exception message: {e2.Message}");
                            TelemetryInstance.TrackException(TelemetryErrorName.RenewLeasesRollback, e2, this._telemetryProps);
                        }
                    }
                    finally
                    {
                        // Do we want to update this count even in the case of a failure to renew the leases? Probably,
                        // because the count is simply meant to indicate how much time the other thread has spent processing
                        // changes essentially.
                        this._leaseRenewalCount += 1;

                        // If this thread has been cancelled, then the _cancellationTokenSourceExecutor could have already
                        // been disposed so shouldn't cancel it.
                        if (this._leaseRenewalCount == MaxLeaseRenewalCount && !token.IsCancellationRequested)
                        {
                            this._logger.LogWarning("Call to execute the function (TryExecuteAsync) seems to be stuck, so it is being cancelled");

                            // If we keep renewing the leases, the thread responsible for processing the changes is stuck.
                            // If it's stuck, it has to be stuck in the function execution call (I think), so we should
                            // cancel the call.
                            this._cancellationTokenSourceExecutor.Cancel();
                            this._cancellationTokenSourceExecutor = new CancellationTokenSource();
                        }
                    }
                }
            }

            // Want to always release the lock at the end, even if renewing the leases failed.
            this._logger.LogDebugWithThreadId("ReleaseRowsLock - RenewLeases");
            this._rowsLock.Release();
        }

        /// <summary>
        /// Resets the in-memory state of the change monitor and sets it to start polling for changes again.
        /// </summary>
        private async Task ClearRowsAsync()
        {
            this._logger.LogDebugWithThreadId("BEGIN WaitRowsLock - ClearRows");
            await this._rowsLock.WaitAsync();
            this._logger.LogDebugWithThreadId("END WaitRowsLock - ClearRows");

            this._leaseRenewalCount = 0;
            this._state = State.CheckingForChanges;
            this._rowsToProcess = new List<IReadOnlyDictionary<string, object>>();

            this._logger.LogDebugWithThreadId("ReleaseRowsLock - ClearRows");
            this._rowsLock.Release();
        }

        /// <summary>
        /// Releases the leases held on "_rowsToRelease" and updates the state tables with the latest sync version we've processed up to.
        /// </summary>
        /// <returns></returns>
        private async Task ReleaseLeasesAsync(SqlConnection connection, CancellationToken token)
        {
            if (this._rowsToRelease.Count > 0)
            {
                long newLastSyncVersion = this.RecomputeLastSyncVersion();
                bool retrySucceeded = false;

                for (int retryCount = 1; retryCount <= MaxRetryReleaseLeases && !retrySucceeded; retryCount++)
                {
                    var transactionSw = Stopwatch.StartNew();
                    long releaseLeasesDurationMs = 0L, updateLastSyncVersionDurationMs = 0L;

                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead))
                    {
                        try
                        {
                            // Release the leases held on "_rowsToRelease".
                            using (SqlCommand releaseLeasesCommand = this.BuildReleaseLeasesCommand(connection, transaction))
                            {
                                this._logger.LogDebugWithThreadId($"BEGIN ReleaseLeases Query={releaseLeasesCommand.CommandText}");
                                var commandSw = Stopwatch.StartNew();
                                int rowsUpdated = await releaseLeasesCommand.ExecuteNonQueryAsync(token);
                                releaseLeasesDurationMs = commandSw.ElapsedMilliseconds;
                                this._logger.LogDebugWithThreadId($"END ReleaseLeases Duration={releaseLeasesDurationMs}ms RowsUpdated={rowsUpdated}");
                            }

                            // Update the global state table if we have processed all changes with ChangeVersion <= newLastSyncVersion,
                            // and clean up the leases table to remove all rows with ChangeVersion <= newLastSyncVersion.
                            using (SqlCommand updateTablesPostInvocationCommand = this.BuildUpdateTablesPostInvocation(connection, transaction, newLastSyncVersion))
                            {
                                this._logger.LogDebugWithThreadId($"BEGIN UpdateTablesPostInvocation Query={updateTablesPostInvocationCommand.CommandText}");
                                var commandSw = Stopwatch.StartNew();
                                await updateTablesPostInvocationCommand.ExecuteNonQueryAsync(token);
                                updateLastSyncVersionDurationMs = commandSw.ElapsedMilliseconds;
                                this._logger.LogDebugWithThreadId($"END UpdateTablesPostInvocation Duration={updateLastSyncVersionDurationMs}ms");
                            }
                            transaction.Commit();

                            var measures = new Dictionary<TelemetryMeasureName, double>
                            {
                                [TelemetryMeasureName.ReleaseLeasesDurationMs] = releaseLeasesDurationMs,
                                [TelemetryMeasureName.UpdateLastSyncVersionDurationMs] = updateLastSyncVersionDurationMs,
                                [TelemetryMeasureName.TransactionDurationMs] = transactionSw.ElapsedMilliseconds,
                            };

                            TelemetryInstance.TrackEvent(TelemetryEventName.ReleaseLeases, this._telemetryProps, measures);
                            retrySucceeded = true;
                            this._rowsToRelease = new List<IReadOnlyDictionary<string, object>>();
                        }
                        catch (Exception ex)
                        {
                            if (retryCount < MaxRetryReleaseLeases)
                            {
                                this._logger.LogError($"Failed to execute SQL commands to release leases in attempt: {retryCount} for table '{this._userTable.FullName}' due to exception: {ex.GetType()}. Exception message: {ex.Message}");

                                var measures = new Dictionary<TelemetryMeasureName, double>
                                {
                                    [TelemetryMeasureName.RetryAttemptNumber] = retryCount,
                                };

                                TelemetryInstance.TrackException(TelemetryErrorName.ReleaseLeases, ex, this._telemetryProps, measures);
                            }
                            else
                            {
                                this._logger.LogError($"Failed to release leases for table '{this._userTable.FullName}' after {MaxRetryReleaseLeases} attempts due to exception: {ex.GetType()}. Exception message: {ex.Message}");
                                TelemetryInstance.TrackException(TelemetryErrorName.ReleaseLeasesNoRetriesLeft, ex, this._telemetryProps);
                            }

                            try
                            {
                                transaction.Rollback();
                            }
                            catch (Exception ex2)
                            {
                                this._logger.LogError($"Failed to rollback transaction due to exception: {ex2.GetType()}. Exception message: {ex2.Message}");
                                TelemetryInstance.TrackException(TelemetryErrorName.ReleaseLeasesRollback, ex2, this._telemetryProps);
                            }
                        }
                    }
                }
            }
            await this.ClearRowsAsync();
        }

        /// <summary>
        /// Computes the version number that can be potentially used as the new LastSyncVersion in the global state table.
        /// </summary>
        private long RecomputeLastSyncVersion()
        {
            var changeVersionSet = new SortedSet<long>();
            foreach (IReadOnlyDictionary<string, object> row in this._rowsToRelease)
            {
                string changeVersion = row[SysChangeVersionColumnName].ToString();
                changeVersionSet.Add(long.Parse(changeVersion, CultureInfo.InvariantCulture));
            }

            // The batch of changes are gotten in ascending order of the version number.
            // With this, it is ensured that if there are multiple version numbers in the changeVersionSet,
            // all the other rows with version numbers less than the highest should have either been processed or
            // have leases acquired on them by another worker.
            // Therefore, if there are more than one version numbers in the set, return the second highest one. Otherwise, return
            // the only version number in the set.
            // Also this LastSyncVersion is actually updated in the GlobalState table only after verifying that the changes with
            // changeVersion <= newLastSyncVersion have been processed in BuildUpdateTablesPostInvocation query.
            long lastSyncVersion = changeVersionSet.ElementAt(changeVersionSet.Count > 1 ? changeVersionSet.Count - 2 : 0);
            this._logger.LogDebugWithThreadId($"RecomputeLastSyncVersion. LastSyncVersion={lastSyncVersion} ChangeVersionSet={string.Join(",", changeVersionSet)}");
            return lastSyncVersion;
        }

        /// <summary>
        /// Builds up the list of <see cref="SqlChange{T}"/> passed to the user's triggered function based on the data
        /// stored in "_rows". If any of the changes correspond to a deleted row, then the <see cref="SqlChange.Item">
        /// will be populated with only the primary key values of the deleted row.
        /// </summary>
        /// <returns>The list of changes</returns>
        private IReadOnlyList<SqlChange<T>> ProcessChanges()
        {
            this._logger.LogDebugWithThreadId("BEGIN ProcessChanges");
            var changes = new List<SqlChange<T>>();
            foreach (IReadOnlyDictionary<string, object> row in this._rowsToProcess)
            {
                SqlChangeOperation operation = GetChangeOperation(row);

                // If the row has been deleted, there is no longer any data for it in the user table. The best we can do
                // is populate the row-item with the primary key values of the row.
                Dictionary<string, object> item = operation == SqlChangeOperation.Delete
                    ? this._primaryKeyColumns.ToDictionary(col => col.name, col => row[col.name])
                    : this._userTableColumns.ToDictionary(col => col, col => row[col]);

                changes.Add(new SqlChange<T>(operation, JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(item))));
            }
            this._logger.LogDebugWithThreadId("END ProcessChanges");
            return changes;
        }

        /// <summary>
        /// Gets the change associated with this row (either an insert, update or delete).
        /// </summary>
        /// <param name="row">The (combined) row from the change table and leases table</param>
        /// <exception cref="InvalidDataException">Thrown if the value of the "SYS_CHANGE_OPERATION" column is none of "I", "U", or "D"</exception>
        /// <returns>SqlChangeOperation.Insert for an insert, SqlChangeOperation.Update for an update, and SqlChangeOperation.Delete for a delete</returns>
        private static SqlChangeOperation GetChangeOperation(IReadOnlyDictionary<string, object> row)
        {
            string operation = row["SYS_CHANGE_OPERATION"].ToString();
            switch (operation)
            {
                case "I": return SqlChangeOperation.Insert;
                case "U": return SqlChangeOperation.Update;
                case "D": return SqlChangeOperation.Delete;
                default: throw new InvalidDataException($"Invalid change type encountered in change table row: {row}");
            };
        }

        /// <summary>
        /// Builds the command to update the global state table in the case of a new minimum valid version number.
        /// Sets the LastSyncVersion for this _userTable to be the new minimum valid version number.
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildUpdateTablesPreInvocation(SqlConnection connection, SqlTransaction transaction)
        {
            string updateTablesPreInvocationQuery = $@"
                {AppLockStatements}

                DECLARE @min_valid_version bigint;
                SET @min_valid_version = CHANGE_TRACKING_MIN_VALID_VERSION({this._userTableId});

                DECLARE @last_sync_version bigint;
                SELECT @last_sync_version = LastSyncVersion
                FROM {GlobalStateTableName}
                WHERE UserFunctionID = '{this._userFunctionId}' AND UserTableID = {this._userTableId};

                IF @last_sync_version < @min_valid_version
                    UPDATE {GlobalStateTableName}
                    SET LastSyncVersion = @min_valid_version
                    WHERE UserFunctionID = '{this._userFunctionId}' AND UserTableID = {this._userTableId};
            ";

            return new SqlCommand(updateTablesPreInvocationQuery, connection, transaction);
        }

        /// <summary>
        /// Builds the query to check for changes on the user's table (<see cref="RunChangeConsumptionLoopAsync()"/>).
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildGetChangesCommand(SqlConnection connection, SqlTransaction transaction)
        {
            string selectList = string.Join(", ", this._userTableColumns.Select(col => this._primaryKeyColumns.Select(c => c.name).Contains(col) ? $"c.{col.AsBracketQuotedString()}" : $"u.{col.AsBracketQuotedString()}"));
            string userTableJoinCondition = string.Join(" AND ", this._primaryKeyColumns.Select(col => $"c.{col.name.AsBracketQuotedString()} = u.{col.name.AsBracketQuotedString()}"));
            string leasesTableJoinCondition = string.Join(" AND ", this._primaryKeyColumns.Select(col => $"c.{col.name.AsBracketQuotedString()} = l.{col.name.AsBracketQuotedString()}"));

            string getChangesQuery = $@"
                {AppLockStatements}

                DECLARE @last_sync_version bigint;
                SELECT @last_sync_version = LastSyncVersion
                FROM {GlobalStateTableName}
                WHERE UserFunctionID = '{this._userFunctionId}' AND UserTableID = {this._userTableId};

                SELECT TOP {this._maxBatchSize}
                    {selectList},
                    c.{SysChangeVersionColumnName},
                    c.SYS_CHANGE_OPERATION,
                    l.{LeasesTableChangeVersionColumnName},
                    l.{LeasesTableAttemptCountColumnName},
                    l.{LeasesTableLeaseExpirationTimeColumnName}
                FROM CHANGETABLE(CHANGES {this._userTable.BracketQuotedFullName}, @last_sync_version) AS c
                LEFT OUTER JOIN {this._leasesTableName} AS l ON {leasesTableJoinCondition}
                LEFT OUTER JOIN {this._userTable.BracketQuotedFullName} AS u ON {userTableJoinCondition}
                WHERE
                    (l.{LeasesTableLeaseExpirationTimeColumnName} IS NULL AND
                       (l.{LeasesTableChangeVersionColumnName} IS NULL OR l.{LeasesTableChangeVersionColumnName} < c.{SysChangeVersionColumnName}) OR
                        l.{LeasesTableLeaseExpirationTimeColumnName} < SYSDATETIME()
                    ) AND
                    (l.{LeasesTableAttemptCountColumnName} IS NULL OR l.{LeasesTableAttemptCountColumnName} < {MaxChangeProcessAttemptCount})
                ORDER BY c.{SysChangeVersionColumnName} ASC;";

            return new SqlCommand(getChangesQuery, connection, transaction);
        }

        /// <summary>
        /// Builds the query to get count of unprocessed changes in the user's table. This one mimics the query that is
        /// used by workers to get the changes for processing.
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildGetUnprocessedChangesCommand(SqlConnection connection, SqlTransaction transaction)
        {
            string leasesTableJoinCondition = string.Join(" AND ", this._primaryKeyColumns.Select(col => $"c.{col.name.AsBracketQuotedString()} = l.{col.name.AsBracketQuotedString()}"));

            string getUnprocessedChangesQuery = $@"
                {AppLockStatements}

                DECLARE @last_sync_version bigint;
                SELECT @last_sync_version = LastSyncVersion
                FROM {GlobalStateTableName}
                WHERE UserFunctionID = '{this._userFunctionId}' AND UserTableID = {this._userTableId};

                SELECT COUNT_BIG(*)
                FROM CHANGETABLE(CHANGES {this._userTable.BracketQuotedFullName}, @last_sync_version) AS c
                LEFT OUTER JOIN {this._leasesTableName} AS l ON {leasesTableJoinCondition}
                WHERE
                    (l.{LeasesTableLeaseExpirationTimeColumnName} IS NULL AND
                       (l.{LeasesTableChangeVersionColumnName} IS NULL OR l.{LeasesTableChangeVersionColumnName} < c.{SysChangeVersionColumnName}) OR
                        l.{LeasesTableLeaseExpirationTimeColumnName} < SYSDATETIME()) AND
                    (l.{LeasesTableAttemptCountColumnName} IS NULL OR l.{LeasesTableAttemptCountColumnName} < {MaxChangeProcessAttemptCount});
            ";

            return new SqlCommand(getUnprocessedChangesQuery, connection, transaction);
        }

        /// <summary>
        /// Builds the query to acquire leases on the rows in "_rows" if changes are detected in the user's table
        /// (<see cref="RunChangeConsumptionLoopAsync()"/>).
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <param name="rows">Dictionary representing the table rows on which leases should be acquired</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildAcquireLeasesCommand(SqlConnection connection, SqlTransaction transaction, IReadOnlyList<IReadOnlyDictionary<string, object>> rows)
        {
            // The column definitions to use for the CTE
            IEnumerable<string> cteColumnDefinitions = this._primaryKeyColumns
                .Select(c => $"{c.name.AsBracketQuotedString()} {c.type}")
                // These are the internal column values that we use. Note that we use SYS_CHANGE_VERSION because that's
                // the new version - the _az_func_ChangeVersion has the old version
                .Concat(new string[] { $"{SysChangeVersionColumnName} bigint", $"{LeasesTableAttemptCountColumnName} int" });
            IEnumerable<string> bracketedPrimaryKeys = this._primaryKeyColumns.Select(p => p.name.AsBracketQuotedString());

            // Create the query that the merge statement will match the rows on
            string primaryKeyMatchingQuery = string.Join(" AND ", bracketedPrimaryKeys.Select(key => $"ExistingData.{key} = NewData.{key}"));
            const string acquireLeasesCte = "acquireLeasesCte";
            const string rowDataParameter = "@rowData";
            // Create the merge query that will either update the rows that already exist or insert a new one if it doesn't exist
            string query = $@"
                    {AppLockStatements}

                    WITH {acquireLeasesCte} AS ( SELECT * FROM OPENJSON(@rowData) WITH ({string.Join(",", cteColumnDefinitions)}) )
                    MERGE INTO {this._leasesTableName}
                        AS ExistingData
                    USING {acquireLeasesCte}
                        AS NewData
                    ON
                        {primaryKeyMatchingQuery}
                    WHEN MATCHED THEN
                        UPDATE SET
                        {LeasesTableChangeVersionColumnName} = NewData.{SysChangeVersionColumnName},
                        {LeasesTableAttemptCountColumnName} = ExistingData.{LeasesTableAttemptCountColumnName} + 1,
                        {LeasesTableLeaseExpirationTimeColumnName} = DATEADD(second, {LeaseIntervalInSeconds}, SYSDATETIME())
                    WHEN NOT MATCHED THEN
                        INSERT VALUES ({string.Join(",", bracketedPrimaryKeys.Select(k => $"NewData.{k}"))}, NewData.{SysChangeVersionColumnName}, 1, DATEADD(second, {LeaseIntervalInSeconds}, SYSDATETIME()));";

            var command = new SqlCommand(query, connection, transaction);
            SqlParameter par = command.Parameters.Add(rowDataParameter, SqlDbType.NVarChar, -1);
            string rowData = JsonConvert.SerializeObject(rows);
            par.Value = rowData;
            return command;
        }

        /// <summary>
        /// Builds the query to renew leases on the rows in "_rows" (<see cref="RenewLeasesAsync(CancellationToken)"/>).
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildRenewLeasesCommand(SqlConnection connection, SqlTransaction transaction)
        {
            string matchCondition = string.Join(" OR ", this._rowMatchConditions.Take(this._rowsToProcess.Count));

            string renewLeasesQuery = $@"
                {AppLockStatements}

                UPDATE {this._leasesTableName}
                SET {LeasesTableLeaseExpirationTimeColumnName} = DATEADD(second, {LeaseIntervalInSeconds}, SYSDATETIME())
                WHERE {matchCondition};
            ";

            return this.GetSqlCommandWithParameters(renewLeasesQuery, connection, transaction, this._rowsToProcess);
        }

        /// <summary>
        /// Builds the query to release leases on the rows in "_rowsToRelease" after successful invocation of the user's function
        /// (<see cref="RunChangeConsumptionLoopAsync()"/>).
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildReleaseLeasesCommand(SqlConnection connection, SqlTransaction transaction)
        {
            // The column definitions to use for the CTE
            IEnumerable<string> cteColumnDefinitions = this._primaryKeyColumns
                .Select(c => $"{c.name.AsBracketQuotedString()} {c.type}")
                // Also bring in the SYS_CHANGE_VERSION column to compare against
                .Append($"{SysChangeVersionColumnName} bigint");
            IEnumerable<string> bracketedPrimaryKeys = this._primaryKeyColumns.Select(p => p.name.AsBracketQuotedString());

            // Create the query that the update statement will match the rows on
            string primaryKeyMatchingQuery = string.Join(" AND ", bracketedPrimaryKeys.Select(key => $"l.{key} = cte.{key}"));
            const string releaseLeasesCte = "releaseLeasesCte";
            const string rowDataParameter = "@rowData";

            string releaseLeasesQuery =
$@"{AppLockStatements}

WITH {releaseLeasesCte} AS ( SELECT * FROM OPENJSON(@rowData) WITH ({string.Join(",", cteColumnDefinitions)}) )
UPDATE {this._leasesTableName}
SET
    {LeasesTableChangeVersionColumnName} = cte.{SysChangeVersionColumnName},
    {LeasesTableAttemptCountColumnName} = 0,
    {LeasesTableLeaseExpirationTimeColumnName} = NULL
FROM {this._leasesTableName} l INNER JOIN releaseLeasesCte cte ON {primaryKeyMatchingQuery}
WHERE l.{LeasesTableChangeVersionColumnName} <= cte.{SysChangeVersionColumnName};";

            var command = new SqlCommand(releaseLeasesQuery, connection, transaction);
            SqlParameter par = command.Parameters.Add(rowDataParameter, SqlDbType.NVarChar, -1);
            string rowData = JsonConvert.SerializeObject(this._rowsToRelease);
            par.Value = rowData;
            return command;
        }

        /// <summary>
        /// Builds the command to update the global version number in _globalStateTable after successful invocation of
        /// the user's function. If the global version number is updated, also cleans the leases table and removes all
        /// rows for which ChangeVersion <= newLastSyncVersion.
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <param name="newLastSyncVersion">The new LastSyncVersion to store in the _globalStateTable for this _userTableName</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildUpdateTablesPostInvocation(SqlConnection connection, SqlTransaction transaction, long newLastSyncVersion)
        {
            string leasesTableJoinCondition = string.Join(" AND ", this._primaryKeyColumns.Select(col => $"c.{col.name.AsBracketQuotedString()} = l.{col.name.AsBracketQuotedString()}"));

            string updateTablesPostInvocationQuery = $@"
                {AppLockStatements}

                DECLARE @current_last_sync_version bigint;
                SELECT @current_last_sync_version = LastSyncVersion
                FROM {GlobalStateTableName}
                WHERE UserFunctionID = '{this._userFunctionId}' AND UserTableID = {this._userTableId};

                DECLARE @unprocessed_changes bigint;
                SELECT @unprocessed_changes = COUNT(*) FROM (
                    SELECT c.{SysChangeVersionColumnName}
                    FROM CHANGETABLE(CHANGES {this._userTable.BracketQuotedFullName}, @current_last_sync_version) AS c
                    LEFT OUTER JOIN {this._leasesTableName} AS l ON {leasesTableJoinCondition}
                    WHERE
                        c.{SysChangeVersionColumnName} <= {newLastSyncVersion} AND
                        ((l.{LeasesTableChangeVersionColumnName} IS NULL OR
                           l.{LeasesTableChangeVersionColumnName} != c.{SysChangeVersionColumnName} OR
                           l.{LeasesTableLeaseExpirationTimeColumnName} IS NOT NULL) AND
                        (l.{LeasesTableAttemptCountColumnName} IS NULL OR l.{LeasesTableAttemptCountColumnName} < {MaxChangeProcessAttemptCount}))) AS Changes

                IF @unprocessed_changes = 0 AND @current_last_sync_version < {newLastSyncVersion}
                BEGIN
                    UPDATE {GlobalStateTableName}
                    SET LastSyncVersion = {newLastSyncVersion}
                    WHERE UserFunctionID = '{this._userFunctionId}' AND UserTableID = {this._userTableId};

                    DELETE FROM {this._leasesTableName} WHERE {LeasesTableChangeVersionColumnName} <= {newLastSyncVersion};
                END
            ";

            return new SqlCommand(updateTablesPostInvocationQuery, connection, transaction);
        }

        /// <summary>
        /// Returns SqlCommand with SqlParameters added to it. Each parameter follows the format
        /// (@PrimaryKey_i, PrimaryKeyValue), where @PrimaryKey is the name of a primary key column, and PrimaryKeyValue
        /// is one of the row's value for that column. To distinguish between the parameters of different rows, each row
        /// will have a distinct value of i.
        /// </summary>
        /// <param name="commandText">SQL query string</param>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <param name="rows">Dictionary representing the table rows</param>
        /// <remarks>
        /// Ideally, we would have a map that maps from rows to a list of SqlCommands populated with their primary key
        /// values. The issue with this is that SQL doesn't seem to allow adding parameters to one collection when they
        /// are part of another. So, for example, since the SqlParameters are part of the list in the map, an exception
        /// is thrown if they are also added to the collection of a SqlCommand. The expected behavior seems to be to
        /// rebuild the SqlParameters each time.
        /// </remarks>
        private SqlCommand GetSqlCommandWithParameters(string commandText, SqlConnection connection,
            SqlTransaction transaction, IReadOnlyList<IReadOnlyDictionary<string, object>> rows)
        {
            var command = new SqlCommand(commandText, connection, transaction);

            SqlParameter[] parameters = Enumerable.Range(0, rows.Count)
                .SelectMany(rowIndex => this._primaryKeyColumns.Select((col, colIndex) => new SqlParameter($"@{rowIndex}_{colIndex}", rows[rowIndex][col.name])))
                .ToArray();

            command.Parameters.AddRange(parameters);
            return command;
        }

        private enum State
        {
            CheckingForChanges,
            ProcessingChanges,
            Cleanup
        }
    }
}