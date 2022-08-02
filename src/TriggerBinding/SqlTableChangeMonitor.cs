// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Watches for changes in the user table, invokes user function if changes are found, and manages leases.
    /// </summary>
    /// <typeparam name="T">POCO class representing the row in the user table</typeparam>
    internal sealed class SqlTableChangeMonitor<T> : IDisposable
    {
        public const int BatchSize = 10;
        public const int PollingIntervalInSeconds = 5;
        public const int MaxAttemptCount = 5;

        // Leases are held for approximately (LeaseRenewalIntervalInSeconds * MaxLeaseRenewalCount) seconds. It is
        // required to have at least one of (LeaseIntervalInSeconds / LeaseRenewalIntervalInSeconds) attempts to
        // renew the lease succeed to prevent it from expiring.
        public const int MaxLeaseRenewalCount = 10;
        public const int LeaseIntervalInSeconds = 60;
        public const int LeaseRenewalIntervalInSeconds = 15;

        private readonly string _connectionString;
        private readonly int _userTableId;
        private readonly SqlObject _userTable;
        private readonly string _userFunctionId;
        private readonly string _workerTableName;
        private readonly IReadOnlyList<string> _userTableColumns;
        private readonly IReadOnlyList<string> _primaryKeyColumns;
        private readonly IReadOnlyList<string> _rowMatchConditions;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ILogger _logger;

        private readonly CancellationTokenSource _cancellationTokenSourceCheckForChanges;
        private readonly CancellationTokenSource _cancellationTokenSourceRenewLeases;
        private CancellationTokenSource _cancellationTokenSourceExecutor;

        // The semaphore ensures that mutable class members such as this._rows are accessed by only one thread at a time.
        private readonly SemaphoreSlim _rowsLock;

        private IReadOnlyList<IReadOnlyDictionary<string, string>> _rows;
        private int _leaseRenewalCount;
        private State _state = State.CheckingForChanges;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTableChangeMonitor{T}" />> class.
        /// </summary>
        /// <param name="connectionString">SQL connection string used to connect to user database</param>
        /// <param name="userTableId">SQL object ID of the user table</param>
        /// <param name="userTable"><see cref="SqlObject"> instance created with user table name</param>
        /// <param name="userFunctionId">Unique identifier for the user function</param>
        /// <param name="workerTableName">Name of the worker table</param>
        /// <param name="userTableColumns">List of all column names in the user table</param>
        /// <param name="primaryKeyColumns">List of primary key column names in the user table</param>
        /// <param name="executor">Defines contract for triggering user function</param>
        /// <param name="logger">Facilitates logging of messages</param>
        public SqlTableChangeMonitor(
            string connectionString,
            int userTableId,
            SqlObject userTable,
            string userFunctionId,
            string workerTableName,
            IReadOnlyList<string> userTableColumns,
            IReadOnlyList<string> primaryKeyColumns,
            ITriggeredFunctionExecutor executor,
            ILogger logger)
        {
            _ = !string.IsNullOrEmpty(connectionString) ? true : throw new ArgumentNullException(nameof(connectionString));
            _ = !string.IsNullOrEmpty(userTable.FullName) ? true : throw new ArgumentNullException(nameof(userTable));
            _ = !string.IsNullOrEmpty(userFunctionId) ? true : throw new ArgumentNullException(nameof(userFunctionId));
            _ = !string.IsNullOrEmpty(workerTableName) ? true : throw new ArgumentNullException(nameof(workerTableName));
            _ = userTableColumns ?? throw new ArgumentNullException(nameof(userTableColumns));
            _ = primaryKeyColumns ?? throw new ArgumentNullException(nameof(primaryKeyColumns));
            _ = executor ?? throw new ArgumentNullException(nameof(executor));
            _ = logger ?? throw new ArgumentNullException(nameof(logger));

            this._connectionString = connectionString;
            this._userTableId = userTableId;
            this._userTable = userTable;
            this._userFunctionId = userFunctionId;
            this._workerTableName = workerTableName;
            this._userTableColumns = userTableColumns;
            this._primaryKeyColumns = primaryKeyColumns;

            // Prep search-conditions that will be used besides WHERE clause to match table rows.
            this._rowMatchConditions = Enumerable.Range(0, BatchSize)
                .Select(rowIndex => string.Join(" AND ", this._primaryKeyColumns.Select((col, colIndex) => $"{col.AsBracketQuotedString()} = @{rowIndex}_{colIndex}")))
                .ToList();

            this._executor = executor;
            this._logger = logger;

            this._cancellationTokenSourceCheckForChanges = new CancellationTokenSource();
            this._cancellationTokenSourceRenewLeases = new CancellationTokenSource();
            this._cancellationTokenSourceExecutor = new CancellationTokenSource();

            this._rowsLock = new SemaphoreSlim(1);
            this._rows = new List<IReadOnlyDictionary<string, string>>();
            this._leaseRenewalCount = 0;
            this._state = State.CheckingForChanges;

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
            this._cancellationTokenSourceCheckForChanges.Cancel();
        }

        /// <summary>
        /// Executed once every <see cref="PollingIntervalInSeconds"/> period. If the state of the change monitor is
        /// <see cref="State.CheckingForChanges"/>, then the method query the change/worker tables for changes on the
        /// user's table. If any are found, the state of the change monitor is transitioned to
        /// <see cref="State.ProcessingChanges"/> and the user's function is executed with the found changes. If the
        /// execution is successful, the leases on "_rows" are released and the state transitions to
        /// <see cref="State.CheckingForChanges"/> once again.
        /// </summary>
        private async Task RunChangeConsumptionLoopAsync()
        {
            try
            {
                CancellationToken token = this._cancellationTokenSourceCheckForChanges.Token;

                using (var connection = new SqlConnection(this._connectionString))
                {
                    await connection.OpenAsync(token);

                    // Check for cancellation request only after a cycle of checking and processing of changes completes.
                    while (!token.IsCancellationRequested)
                    {
                        if (this._state == State.CheckingForChanges)
                        {
                            await this.GetTableChangesAsync(token);
                            await this.ProcessTableChangesAsync(token);
                        }

                        await Task.Delay(TimeSpan.FromSeconds(PollingIntervalInSeconds), token);
                    }
                }
            }
            catch (Exception e)
            {
                // Only want to log the exception if it wasn't caused by StopAsync being called, since Task.Delay
                // throws an exception if it's cancelled.
                if (e.GetType() != typeof(TaskCanceledException))
                {
                    this._logger.LogError(e.Message);
                }
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
        /// Queries the change/worker tables to check for new changes on the user's table. If any are found, stores the
        /// change along with the corresponding data from the user table in "_rows".
        /// </summary>
        private async Task GetTableChangesAsync(CancellationToken token)
        {
            try
            {
                using (var connection = new SqlConnection(this._connectionString))
                {
                    await connection.OpenAsync(token);

                    using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
                    {
                        try
                        {
                            // Update the version number stored in the global state table if necessary before using it.
                            using (SqlCommand updateTablesPreInvocationCommand = this.BuildUpdateTablesPreInvocation(connection, transaction))
                            {
                                await updateTablesPreInvocationCommand.ExecuteNonQueryAsync(token);
                            }

                            // Use the version number to query for new changes.
                            using (SqlCommand getChangesCommand = this.BuildGetChangesCommand(connection, transaction))
                            {
                                var rows = new List<IReadOnlyDictionary<string, string>>();
                                using (SqlDataReader reader = await getChangesCommand.ExecuteReaderAsync(token))
                                {
                                    while (await reader.ReadAsync(token))
                                    {
                                        rows.Add(SqlBindingUtilities.BuildDictionaryFromSqlRow(reader));
                                    }
                                }

                                this._rows = rows;
                            }

                            // If changes were found, acquire leases on them.
                            if (this._rows.Count > 0)
                            {
                                using (SqlCommand acquireLeasesCommand = this.BuildAcquireLeasesCommand(connection, transaction))
                                {
                                    await acquireLeasesCommand.ExecuteNonQueryAsync(token);
                                }
                            }
                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            this._logger.LogError($"Failed to query list of changes for table '{this._userTable.FullName}' due to exception: {ex.GetType()}." +
                                $" Exception message: {ex.Message}");

                            try
                            {
                                transaction.Rollback();
                            }
                            catch (Exception ex2)
                            {
                                this._logger.LogError($"Failed to rollback transaction due to exception: {ex2.GetType()}. Exception message: {ex2.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // If there's an exception in any part of the process, we want to clear all of our data in memory and
                // retry checking for changes again.
                this._rows = new List<IReadOnlyDictionary<string, string>>();
                this._logger.LogError($"Failed to check for changes in table '{this._userTable.FullName}' due to exception: {e.GetType()}." +
                    $" Exception message: {e.Message}");
            }
        }

        private async Task ProcessTableChangesAsync(CancellationToken token)
        {
            if (this._rows.Count > 0)
            {
                this._state = State.ProcessingChanges;
                IReadOnlyList<SqlChange<T>> changes = null;

                try
                {
                    // What should we do if this fails? It doesn't make sense to retry since it's not a connection based
                    // thing. We could still try to trigger on the correctly processed changes, but that adds additional
                    // complication because we don't want to release the leases on the incorrectly processed changes.
                    // For now, just give up I guess?
                    changes = this.GetChanges();
                }
                catch (Exception e)
                {
                    this._logger.LogError($"Failed to compose trigger parameter value for table: '{this._userTable.FullName} due to exception: {e.GetType()}." +
                        $" Exception message: {e.Message}");

                    await this.ClearRowsAsync(true);
                }

                if (changes != null)
                {
                    var input = new TriggeredFunctionData() { TriggerValue = changes };
                    FunctionResult result = await this._executor.TryExecuteAsync(input, this._cancellationTokenSourceExecutor.Token);

                    if (result.Succeeded)
                    {
                        await this.ReleaseLeasesAsync(token);
                    }
                    else
                    {
                        // In the future might make sense to retry executing the function, but for now we just let
                        // another worker try.
                        this._logger.LogError($"Failed to trigger user function for table: '{this._userTable.FullName} due to exception: {result.Exception.GetType()}." +
                            $" Exception message: {result.Exception.Message}");

                        await this.ClearRowsAsync(true);
                    }
                }
            }
        }

        /// <summary>
        /// Executed once every <see cref="LeaseTime"/> period. If the state of the change monitor is
        /// <see cref="State.ProcessingChanges"/>, then we will renew the leases held by the change monitor on "_rows".
        /// </summary>
        private async void RunLeaseRenewalLoopAsync()
        {
            try
            {
                CancellationToken token = this._cancellationTokenSourceRenewLeases.Token;

                using (var connection = new SqlConnection(this._connectionString))
                {
                    await connection.OpenAsync(token);

                    while (!token.IsCancellationRequested)
                    {
                        await this._rowsLock.WaitAsync(token);
                        await this.RenewLeasesAsync(connection, token);
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
                    this._logger.LogError(e.Message);
                }
            }
            finally
            {
                this._cancellationTokenSourceRenewLeases.Dispose();
            }
        }

        private async Task RenewLeasesAsync(SqlConnection connection, CancellationToken token)
        {
            try
            {
                if (this._state == State.ProcessingChanges)
                {
                    // I don't think I need a transaction for renewing leases. If this worker reads in a row from the
                    // worker table and determines that it corresponds to its batch of changes, but then that row gets
                    // deleted by a cleanup task, it shouldn't renew the lease on it anyways.
                    using (SqlCommand renewLeasesCommand = this.BuildRenewLeasesCommand(connection))
                    {
                        await renewLeasesCommand.ExecuteNonQueryAsync(token);
                    }
                }
            }
            catch (Exception e)
            {
                // This catch block is necessary so that the finally block is executed even in the case of an exception
                // (see https://docs.microsoft.com/dotnet/csharp/language-reference/keywords/try-finally, third
                // paragraph). If we fail to renew the leases, multiple workers could be processing the same change
                // data, but we have functionality in place to deal with this (see design doc).
                this._logger.LogError($"Failed to renew leases due to exception: {e.GetType()}. Exception message: {e.Message}");
            }
            finally
            {
                if (this._state == State.ProcessingChanges)
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

                // Want to always release the lock at the end, even if renewing the leases failed.
                this._rowsLock.Release();
            }
        }

        /// <summary>
        /// Resets the in-memory state of the change monitor and sets it to start polling for changes again.
        /// </summary>
        /// <param name="acquireLock">True if ClearRowsAsync should acquire the "_rowsLock" (only true in the case of a failure)</param>
        private async Task ClearRowsAsync(bool acquireLock)
        {
            if (acquireLock)
            {
                await this._rowsLock.WaitAsync();
            }

            this._leaseRenewalCount = 0;
            this._state = State.CheckingForChanges;
            this._rows = new List<IReadOnlyDictionary<string, string>>();
            this._rowsLock.Release();
        }

        /// <summary>
        /// Releases the leases held on "_rows".
        /// </summary>
        /// <returns></returns>
        private async Task ReleaseLeasesAsync(CancellationToken token)
        {
            // Don't want to change the "_rows" while another thread is attempting to renew leases on them.
            await this._rowsLock.WaitAsync(token);
            long newLastSyncVersion = this.RecomputeLastSyncVersion();

            try
            {
                using (var connection = new SqlConnection(this._connectionString))
                {
                    await connection.OpenAsync(token);
                    using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
                    {
                        try
                        {
                            // Release the leases held on "_rows".
                            using (SqlCommand releaseLeasesCommand = this.BuildReleaseLeasesCommand(connection, transaction))
                            {
                                await releaseLeasesCommand.ExecuteNonQueryAsync(token);
                            }

                            // Update the global state table if we have processed all changes with ChangeVersion <= newLastSyncVersion,
                            // and clean up the worker table to remove all rows with ChangeVersion <= newLastSyncVersion.
                            using (SqlCommand updateTablesPostInvocationCommand = this.BuildUpdateTablesPostInvocation(connection, transaction, newLastSyncVersion))
                            {
                                await updateTablesPostInvocationCommand.ExecuteNonQueryAsync(token);
                            }

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            this._logger.LogError($"Failed to execute SQL commands to release leases for table '{this._userTable.FullName}' due to exception: {ex.GetType()}." +
                                $" Exception message: {ex.Message}");

                            try
                            {
                                transaction.Rollback();
                            }
                            catch (Exception ex2)
                            {
                                this._logger.LogError($"Failed to rollback transaction due to exception: {ex2.GetType()}. Exception message: {ex2.Message}");
                            }
                        }
                    }
                }

            }
            catch (Exception e)
            {
                // What should we do if releasing the leases fails? We could try to release them again or just wait,
                // since eventually the lease time will expire. Then another thread will re-process the same changes
                // though, so less than ideal. But for now that's the functionality.
                this._logger.LogError($"Failed to release leases for table '{this._userTable.FullName}' due to exception: {e.GetType()}." +
                    $" Exception message: {e.Message}");
            }
            finally
            {
                // Want to do this before releasing the lock in case the renew leases thread wakes up. It will see that
                // the state is checking for changes and not renew the (just released) leases.
                await this.ClearRowsAsync(false);
            }
        }

        /// <summary>
        /// Computes the version number that can be potentially used as the new LastSyncVersion in the global state table.
        /// </summary>
        private long RecomputeLastSyncVersion()
        {
            var changeVersionSet = new SortedSet<long>();
            foreach (IReadOnlyDictionary<string, string> row in this._rows)
            {
                string changeVersion = row["SYS_CHANGE_VERSION"];
                changeVersionSet.Add(long.Parse(changeVersion, CultureInfo.InvariantCulture));
            }

            // If there are more than one version numbers in the set, return the second highest one. Otherwise, return
            // the only version number in the set.
            return changeVersionSet.ElementAt(changeVersionSet.Count > 1 ? changeVersionSet.Count - 2 : 0);
        }

        /// <summary>
        /// Builds up the list of <see cref="SqlChange{T}"/> passed to the user's triggered function based on the data
        /// stored in "_rows". If any of the changes correspond to a deleted row, then the <see cref="SqlChange.Item">
        /// will be populated with only the primary key values of the deleted row.
        /// </summary>
        /// <returns>The list of changes</returns>
        private IReadOnlyList<SqlChange<T>> GetChanges()
        {
            var changes = new List<SqlChange<T>>();
            foreach (IReadOnlyDictionary<string, string> row in this._rows)
            {
                SqlChangeOperation operation = GetChangeOperation(row);

                // If the row has been deleted, there is no longer any data for it in the user table. The best we can do
                // is populate the row-item with the primary key values of the row.
                Dictionary<string, string> item = operation == SqlChangeOperation.Delete
                    ? this._primaryKeyColumns.ToDictionary(col => col, col => row[col])
                    : this._userTableColumns.ToDictionary(col => col, col => row[col]);

                changes.Add(new SqlChange<T>(operation, JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(item))));
            }

            return changes;
        }

        /// <summary>
        /// Gets the change associated with this row (either an insert, update or delete).
        /// </summary>
        /// <param name="row">The (combined) row from the change table and worker table</param>
        /// <exception cref="InvalidDataException">Thrown if the value of the "SYS_CHANGE_OPERATION" column is none of "I", "U", or "D"</exception>
        /// <returns>SqlChangeOperation.Insert for an insert, SqlChangeOperation.Update for an update, and SqlChangeOperation.Delete for a delete</returns>
        private static SqlChangeOperation GetChangeOperation(IReadOnlyDictionary<string, string> row)
        {
            string operation = row["SYS_CHANGE_OPERATION"];
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
                DECLARE @min_valid_version bigint;
                SET @min_valid_version = CHANGE_TRACKING_MIN_VALID_VERSION({this._userTableId});

                DECLARE @last_sync_version bigint;
                SELECT @last_sync_version = LastSyncVersion
                FROM {SqlTriggerConstants.GlobalStateTableName}
                WHERE UserFunctionID = '{this._userFunctionId}' AND UserTableID = {this._userTableId};
                
                IF @last_sync_version < @min_valid_version
                    UPDATE {SqlTriggerConstants.GlobalStateTableName}
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
            string selectList = string.Join(", ", this._userTableColumns.Select(col => this._primaryKeyColumns.Contains(col) ? $"c.{col.AsBracketQuotedString()}" : $"u.{col.AsBracketQuotedString()}"));
            string userTableJoinCondition = string.Join(" AND ", this._primaryKeyColumns.Select(col => $"c.{col.AsBracketQuotedString()} = u.{col.AsBracketQuotedString()}"));
            string workerTableJoinCondition = string.Join(" AND ", this._primaryKeyColumns.Select(col => $"c.{col.AsBracketQuotedString()} = w.{col.AsBracketQuotedString()}"));

            string getChangesQuery = $@"
                DECLARE @last_sync_version bigint;
                SELECT @last_sync_version = LastSyncVersion
                FROM {SqlTriggerConstants.GlobalStateTableName}
                WHERE UserFunctionID = '{this._userFunctionId}' AND UserTableID = {this._userTableId};

                SELECT TOP {BatchSize}
                    {selectList},
                    c.SYS_CHANGE_VERSION, c.SYS_CHANGE_OPERATION,
                    w.ChangeVersion, w.AttemptCount, w.LeaseExpirationTime
                FROM CHANGETABLE(CHANGES {this._userTable.BracketQuotedFullName}, @last_sync_version) AS c
                LEFT OUTER JOIN {this._workerTableName} AS w ON {workerTableJoinCondition}
                LEFT OUTER JOIN {this._userTable.BracketQuotedFullName} AS u ON {userTableJoinCondition}
                WHERE
                    (w.LeaseExpirationTime IS NULL AND (w.ChangeVersion IS NULL OR w.ChangeVersion < c.SYS_CHANGE_VERSION) OR
                        w.LeaseExpirationTime < SYSDATETIME()) AND
                    (w.AttemptCount IS NULL OR w.AttemptCount < {MaxAttemptCount})
                ORDER BY c.SYS_CHANGE_VERSION ASC;
            ";

            return new SqlCommand(getChangesQuery, connection, transaction);
        }

        /// <summary>
        /// Builds the query to acquire leases on the rows in "_rows" if changes are detected in the user's table
        /// (<see cref="RunChangeConsumptionLoopAsync()"/>).
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildAcquireLeasesCommand(SqlConnection connection, SqlTransaction transaction)
        {
            var acquireLeasesQuery = new StringBuilder();

            for (int rowIndex = 0; rowIndex < this._rows.Count; rowIndex++)
            {
                string valuesList = string.Join(", ", this._primaryKeyColumns.Select((_, colIndex) => $"@{rowIndex}_{colIndex}"));
                string changeVersion = this._rows[rowIndex]["SYS_CHANGE_VERSION"];

                acquireLeasesQuery.Append($@"
                    IF NOT EXISTS (SELECT * FROM {this._workerTableName} WITH (XLOCK) WHERE {this._rowMatchConditions[rowIndex]})
                        INSERT INTO {this._workerTableName}
                        VALUES ({valuesList}, {changeVersion}, 1, DATEADD(second, {LeaseIntervalInSeconds}, SYSDATETIME()));
                    ELSE
                        UPDATE {this._workerTableName}
                        SET
                            ChangeVersion = {changeVersion},
                            AttemptCount = AttemptCount + 1,
                            LeaseExpirationTime = DATEADD(second, {LeaseIntervalInSeconds}, SYSDATETIME())
                        WHERE {this._rowMatchConditions[rowIndex]};
                ");
            }

            return this.GetSqlCommandWithParameters(acquireLeasesQuery.ToString(), connection, transaction);
        }

        /// <summary>
        /// Builds the query to renew leases on the rows in "_rows" (<see cref="RenewLeasesAsync(CancellationToken)"/>).
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildRenewLeasesCommand(SqlConnection connection)
        {
            string matchCondition = string.Join(" OR ", this._rowMatchConditions.Take(this._rows.Count));

            string renewLeasesQuery = $@"
                UPDATE {this._workerTableName}
                SET LeaseExpirationTime = DATEADD(second, {LeaseIntervalInSeconds}, SYSDATETIME())
                WHERE {matchCondition};
            ";

            return this.GetSqlCommandWithParameters(renewLeasesQuery, connection, null);
        }

        /// <summary>
        /// Builds the query to release leases on the rows in "_rows" after successful invocation of the user's function
        /// (<see cref="RunChangeConsumptionLoopAsync()"/>).
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildReleaseLeasesCommand(SqlConnection connection, SqlTransaction transaction)
        {
            var releaseLeasesQuery = new StringBuilder("DECLARE @current_change_version bigint;\n");

            for (int rowIndex = 0; rowIndex < this._rows.Count; rowIndex++)
            {
                string changeVersion = this._rows[rowIndex]["SYS_CHANGE_VERSION"];

                releaseLeasesQuery.Append($@"
                    SELECT @current_change_version = ChangeVersion
                    FROM {this._workerTableName} WITH (UPDLOCK)
                    WHERE {this._rowMatchConditions[rowIndex]};

                    IF @current_change_version <= {changeVersion}
                        UPDATE {this._workerTableName}
                        SET ChangeVersion = {changeVersion}, AttemptCount = 0, LeaseExpirationTime = NULL
                        WHERE {this._rowMatchConditions[rowIndex]};
                ");
            }

            return this.GetSqlCommandWithParameters(releaseLeasesQuery.ToString(), connection, transaction);
        }

        /// <summary>
        /// Builds the command to update the global version number in _globalStateTable after successful invocation of
        /// the user's function. If the global version number is updated, also cleans the worker table and removes all
        /// rows for which ChangeVersion <= newLastSyncVersion.
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <param name="newLastSyncVersion">The new LastSyncVersion to store in the _globalStateTable for this _userTableName</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildUpdateTablesPostInvocation(SqlConnection connection, SqlTransaction transaction, long newLastSyncVersion)
        {
            string workerTableJoinCondition = string.Join(" AND ", this._primaryKeyColumns.Select(col => $"c.{col.AsBracketQuotedString()} = w.{col.AsBracketQuotedString()}"));

            string updateTablesPostInvocationQuery = $@"
                DECLARE @current_last_sync_version bigint;
                SELECT @current_last_sync_version = LastSyncVersion
                FROM {SqlTriggerConstants.GlobalStateTableName}
                WHERE UserFunctionID = '{this._userFunctionId}' AND UserTableID = {this._userTableId};

                DECLARE @unprocessed_changes bigint;
                SELECT @unprocessed_changes = COUNT(*) FROM (
                    SELECT c.SYS_CHANGE_VERSION
                    FROM CHANGETABLE(CHANGES {this._userTable.BracketQuotedFullName}, @current_last_sync_version) AS c
                    LEFT OUTER JOIN {this._workerTableName} AS w WITH (TABLOCKX) ON {workerTableJoinCondition}
                    WHERE
                        c.SYS_CHANGE_VERSION <= {newLastSyncVersion} AND
                        ((w.ChangeVersion IS NULL OR w.ChangeVersion != c.SYS_CHANGE_VERSION OR w.LeaseExpirationTime IS NOT NULL) AND
                        (w.AttemptCount IS NULL OR w.AttemptCount < {MaxAttemptCount}))) AS Changes

                IF @unprocessed_changes = 0 AND @current_last_sync_version < {newLastSyncVersion}
                BEGIN
                    UPDATE {SqlTriggerConstants.GlobalStateTableName}
                    SET LastSyncVersion = {newLastSyncVersion}
                    WHERE UserFunctionID = '{this._userFunctionId}' AND UserTableID = {this._userTableId};

                    DELETE FROM {this._workerTableName} WHERE ChangeVersion <= {newLastSyncVersion};
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
        /// <remarks>
        /// Ideally, we would have a map that maps from rows to a list of SqlCommands populated with their primary key
        /// values. The issue with this is that SQL doesn't seem to allow adding parameters to one collection when they
        /// are part of another. So, for example, since the SqlParameters are part of the list in the map, an exception
        /// is thrown if they are also added to the collection of a SqlCommand. The expected behavior seems to be to
        /// rebuild the SqlParameters each time.
        /// </remarks>
        private SqlCommand GetSqlCommandWithParameters(string commandText, SqlConnection connection, SqlTransaction transaction)
        {
            var command = new SqlCommand(commandText, connection, transaction);

            SqlParameter[] parameters = Enumerable.Range(0, this._rows.Count)
                .SelectMany(rowIndex => this._primaryKeyColumns.Select((col, colIndex) => new SqlParameter($"@{rowIndex}_{colIndex}", this._rows[rowIndex][col])))
                .ToArray();

            command.Parameters.AddRange(parameters);
            return command;
        }

        private enum State
        {
            CheckingForChanges,
            ProcessingChanges,
        }
    }
}