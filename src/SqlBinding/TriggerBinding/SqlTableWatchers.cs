// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SqlBinding.TriggerBinding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SqlBinding.TriggerBinding.ScaleRecommendation;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    public class SqlTableWatchers
    {
        private static string[] variableLengthTypes = new string[] { "varchar", "nvarchar", "nchar", "char", "binary", "varbinary" };
        private static string[] variablePrecisionTypes = new string[] { "numeric", "decimal" };

        public class SqlPerformanceMonitor
        {
            private string _workerTable;
            private readonly string _globalStateTable;
            private readonly string _userTable;
            private readonly string _connectionString;
            
            private readonly Dictionary<string, string> _primaryKeys;
            private string _leftOuterJoinWorkerTable;
            private readonly ILogger _logger;

            /// <summary>
            /// Initializes a new instance of the <see cref="SqlPerformanceMonitor"> class
            /// </summary>
            /// <param name="connectionString">
            /// The SQL connection string used to connect to the user's database
            /// </param>
            /// <param name="table"> 
            /// The name of the user table that changes are being tracked on
            /// </param>
            /// <exception cref="ArgumentException">
            /// Thrown if table or connectionString are null or empty
            /// </exception>
            /// <exception cref="ArgumentNullException">
            /// Thrown if the logger is null
            /// </exception>
            public SqlPerformanceMonitor(string table, string connectionString, ILogger logger)
            {
                if (string.IsNullOrEmpty(table))
                {
                    throw new ArgumentException("User table name cannot be null or empty");
                }
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new ArgumentException("SQL connection string cannot be null or empty");
                }

                _connectionString = connectionString;
                _userTable = SqlBindingUtilities.NormalizeTableName(table);
                _globalStateTable = "[az_func].[Global_State_Table]";
                _primaryKeys = new Dictionary<string, string>();
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            /// <summary>
            /// Starts the watcher which creates the necessary tables to determine metrics for the changes occurring to the user table
            /// </summary>
            /// <returns></returns>
            public async Task StartAsync()
            {
                _workerTable = await CreateWorkerTablesAsync(_connectionString, _primaryKeys, null, _userTable, _globalStateTable, _logger);
            }

            /// <summary>
            /// Makes a scale recommendation based on the current number of unprocessed changes for the user table and the current 
            /// worker count
            /// </summary>
            /// <param name="workerCount">
            /// The number of workers currently processing changes for this user table
            /// </param>
            /// <returns>
            /// A <see cref="SqlHeartbeat"/> containing the scale recommendation as well as additional metrics for this table
            /// </returns>
            public async Task<SqlHeartbeat> MakeScaleRecommendation(int workerCount)
            {
                long unprocessedChanges = await GetUnprocessedChanges();
                if (unprocessedChanges > 0)
                {
                    if (workerCount == 0)
                    {
                        return new SqlHeartbeat(unprocessedChanges, 
                            new ScaleRecommendation(ScaleAction.AddWorker, keepWorkersAlive: true, reason: "First worker"));
                    }
                    // Should probably only add worker in some cases, like if unprocessedChanges / BatchSize - workerCount > threshold.
                    // Otherwise just keep workers alive but do nothing
                    return new SqlHeartbeat(unprocessedChanges, 
                        new ScaleRecommendation(ScaleAction.AddWorker, keepWorkersAlive: true, reason: $"Number of unprocessed changes is {unprocessedChanges}"));
                }
                else if (unprocessedChanges == 0)
                {
                    return new SqlHeartbeat(unprocessedChanges, new ScaleRecommendation(
                        scaleAction: workerCount > 0 ? ScaleAction.RemoveWorker : ScaleAction.None,
                        keepWorkersAlive: false,
                        reason: "No unprocessed changes for user table"));
                }
                else
                {
                    throw new Exception($"Failed to get the number of unprocessed changes for user table {_userTable}");
                }
            }

            /// <summary>
            /// Returns the number of unprocessed changes for the user table
            /// </summary>
            /// <returns>
            /// The number of unprocessed changes, or -1 if the query to get the unprocessed changes fails
            /// </returns>
            private async Task<long> GetUnprocessedChanges()
            {
                long unprocessedChanges = -1;
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    // If we want to avoid deadlocks, perhaps it's okay to make the transaction level ReadUncommitted
                    // We would get less accurate results, but we wouldn't be competing for table locks with other workers
                    using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted))
                    {
                        // Update the version number stored in the global state table if necessary before using it 
                        using (SqlCommand updateGlobalStateTableCommand = BuildUpdateGlobalStateTableCommand(connection, transaction, _userTable, _workerTable, _globalStateTable))
                        {
                            await updateGlobalStateTableCommand.ExecuteNonQueryAsync();
                        }

                        // Use the version number to query for unprocessed changes
                        using (SqlCommand getChangesCommand = BuildGetUnprocessedChangesCommand(connection, transaction))
                        {
                            using (SqlDataReader reader = await getChangesCommand.ExecuteReaderAsync())
                            {

                                if (await reader.ReadAsync())
                                {
                                    unprocessedChanges = reader.GetInt64(0);
                                }
                            }
                        }
                    }
                }
                // If the query somehow didn't return any results, we return -1 to indicate a failure
                return unprocessedChanges;
            }

            /// <summary>
            /// Builds the query to check for how many unprocessed changes currently exist for the user's table
            /// </summary>
            /// <param name="connection">The connection to add to the returned SqlCommand</param>
            /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildGetUnprocessedChangesCommand(SqlConnection connection, SqlTransaction transaction)
            {
                if (string.IsNullOrEmpty(_leftOuterJoinWorkerTable))
                {
                    _leftOuterJoinWorkerTable = string.Join(" AND ", _primaryKeys.Keys.Select(key => $"c.{key} = w.{key}"));
                }

                // COUNT_BIG returns a bigint, which is composed of 8 bytes, not 4, in the case that there are a lot of unprocessed changes
                var getChangesQuery =
                    $"DECLARE @version bigint;\n" +
                    $"SET @version = CHANGE_TRACKING_MIN_VALID_VERSION(OBJECT_ID(\'{_userTable}\'));\n" +
                    $"SELECT COUNT_BIG(*)\n" +
                    $"FROM\n" +
                    $"(SELECT c.SYS_CHANGE_VERSION, w.LeaseExpirationTime, w.DequeueCount, w.VersionNumber\n" +
                    $"FROM CHANGETABLE (CHANGES {_userTable}, @version) AS c\n" +
                    $"LEFT OUTER JOIN {_workerTable} AS w ON {_leftOuterJoinWorkerTable}) AS CHANGES\n" +
                    $"WHERE (Changes.LeaseExpirationTime IS NULL AND\n" +
                    $"(Changes.VersionNumber IS NULL OR Changes.VersionNumber < Changes.SYS_CHANGE_VERSION)\n" +
                    $"OR Changes.LeaseExpirationTime < SYSDATETIME())\n" +
                    $"AND (Changes.DequeueCount IS NULL OR Changes.DequeueCount < {SqlTriggerConstants.MaxDequeueCount})";

                return new SqlCommand(getChangesQuery, connection, transaction);
            }
        }

        /// <summary>
        /// Periodically polls SQL's change table to determine if any new changes have occurred to a user's table
        /// </summary>
        /// <remarks>
        /// Note that there is no possiblity of SQL injection in the raw queries we generate in the Build...Command methods.
        /// All parameters that involve inserting data from a user table are sanitized
        /// All other parameters are generated exclusively using information about the user table's schema (such as primary key column names),
        /// data stored in SQL's internal change table, or data stored in our own worker table.
        /// </remarks>
        /// <typeparam name="T">A user-defined POCO that represents a row of the user's table</typeparam>
        public class SqlTableChangeMonitor<T>
        {
            private string _workerTable;
            private readonly string _globalStateTable;
            private readonly string _userTable;
            private readonly string _connectionString;
            private readonly ITriggeredFunctionExecutor _executor;
            private readonly ILogger _logger;
            private CancellationTokenSource _cancellationTokenSourceExecutor;
            private readonly CancellationTokenSource _cancellationTokenSourceCheckForChanges;
            private readonly CancellationTokenSource _cancellationTokenSourceRenewLeases;

            // It should be impossible for multiple threads to access these at the same time because of the semaphore we use
            private readonly List<Dictionary<string, string>> _rows;
            private readonly List<string> _userTableColumns;
            private readonly List<string> _whereChecks;
            private readonly Dictionary<string, string> _primaryKeys;
            private readonly Dictionary<string, string> _queryStrings;

            private readonly SemaphoreSlim _rowsLock;
            private State _state;
            private int _leaseRenewalCount;

            /// <summary>
            /// Initializes a new instance of the <see cref="SqlTableChangeMonitor<typeparamref name="T"/>> class
            /// </summary>
            /// <param name="connectionString">
            /// The SQL connection string used to connect to the user's database
            /// </param>
            /// <param name="table"> 
            /// The name of the user table that changes are being tracked on
            /// </param>
            /// <param name="executor">
            /// Used to execute the user's function when changes are detected on "table"
            /// </param>
            /// <exception cref="ArgumentNullException">
            /// Thrown if the executor or logger is null
            /// </exception>
            /// <exception cref="ArgumentException">
            /// Thrown if table or connectionString are null or empty
            /// </exception>
            public SqlTableChangeMonitor(string table, string connectionString, ITriggeredFunctionExecutor executor, ILogger logger)
            {
                if (string.IsNullOrEmpty(table))
                {
                    throw new ArgumentException("User table name cannot be null or empty");
                }
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new ArgumentException("SQL connection string cannot be null or empty");
                }

                _connectionString = connectionString;
                _executor = executor ?? throw new ArgumentNullException(nameof(executor));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _userTable = SqlBindingUtilities.NormalizeTableName(table);
                _globalStateTable = "[az_func].[Global_State_Table]";

                _cancellationTokenSourceExecutor = new CancellationTokenSource();
                _cancellationTokenSourceCheckForChanges = new CancellationTokenSource();
                _cancellationTokenSourceRenewLeases = new CancellationTokenSource();
                _rowsLock = new SemaphoreSlim(1);

                _rows = new List<Dictionary<string, string>>();
                _userTableColumns = new List<string>();
                _whereChecks = new List<string>();
                _queryStrings = new Dictionary<string, string>();
                _primaryKeys = new Dictionary<string, string>();
            }

            /// <summary>
            /// Starts the watcher which begins polling for changes on the user's table specified in the constructor
            /// </summary>
            /// <returns></returns>
            public async Task StartAsync()
            {
                _workerTable = await CreateWorkerTablesAsync(_connectionString, _primaryKeys, _userTableColumns, _userTable, _globalStateTable, _logger);
                #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Task.Run(() =>
                {
                    CheckForChangesAsync(_cancellationTokenSourceCheckForChanges.Token);
                    RenewLeasesAsync(_cancellationTokenSourceRenewLeases.Token);
                });
                #pragma warning restore CS4014
            }

            /// <summary>
            /// Stops the watcher which stops polling for changes on the user's table.
            /// If the watcher is currently executing a set of changes, it is only stopped
            /// once execution is finished and the user's function is triggered (whether or not
            /// the trigger is successful) 
            /// </summary>
            /// <returns></returns>
            public void Stop()
            {
                _cancellationTokenSourceCheckForChanges.Cancel();
            }

            /// <summary>
            /// Executed once every <see cref="SqlTriggerConstants.LeaseTime"/> period. 
            /// If the state of the watcher is <see cref="State.ProcessingChanges"/>, then 
            /// we will renew the leases held by the watcher on "_rows"
            /// </summary>
            /// <param name="token">
            /// If the token is cancelled, leases are no longer renewed
            /// </param>
            private async void RenewLeasesAsync(CancellationToken token)
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        await _rowsLock.WaitAsync();
                        try
                        {
                            if (_state == State.ProcessingChanges)
                            {
                                await RenewLeasesAsync();
                            }
                        }
                        catch (Exception e)
                        {
                            // This catch block is necessary so that the finally block is executed even in the case of an exception
                            // (see https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/try-finally, third paragraph)
                            // If we fail to renew the leases, multiple workers could be processing the same change data, but we have functionality
                            // in place to deal with this (see design doc)
                            _logger.LogError($"Failed to renew leases due to error: {e.Message}");
                        }
                        finally
                        {
                            if (_state == State.ProcessingChanges)
                            {
                                // Do we want to update this count even in the case of a failure to renew the leases? Probably, because
                                // the count is simply meant to indicate how much time the other thread has spent processing changes essentially
                                _leaseRenewalCount++;
                                // If this thread has been cancelled, then the _cancellationTokenSourceExecutor could have already been disposed so
                                // shouldn't cancel it
                                if (_leaseRenewalCount == SqlTriggerConstants.MaxLeaseRenewalCount && !token.IsCancellationRequested)
                                {
                                    // If we keep renewing the leases, the thread responsible for processing the changes is stuck
                                    // If it's stuck, it has to be stuck in the function execution call (I think), so we should cancel the call
                                    _logger.LogWarning($"Call to execute the function (TryExecuteAsync) seems to be stuck, so it is being cancelled");
                                    _cancellationTokenSourceExecutor.Cancel();
                                    _cancellationTokenSourceExecutor.Dispose();
                                    _cancellationTokenSourceExecutor = new CancellationTokenSource();
                                }
                            }
                            // Want to always release the lock at the end, even if renewing the leases failed
                            _rowsLock.Release();
                        }
                        // Want to make sure to renew the leases before they expire, so we renew them twice per lease period
                        await Task.Delay(SqlTriggerConstants.LeaseTime / 2 * 1000, token);
                    }
                }
                catch (Exception e)
                {
                    // Only want to log the exception if it wasn't caused by StopAsync being called, since Task.Delay throws an exception
                    // if it's cancelled
                    if (e.GetType() != typeof(TaskCanceledException))
                    {
                        _logger.LogError(e.Message);
                    }
                }
                finally
                {
                    _cancellationTokenSourceRenewLeases.Dispose();
                }
            }

            /// <summary>
            /// Executed once every <see cref="SqlTriggerConstants.PollingInterval"/> period. If the state of the watcher is <see cref="State.CheckingForChanges"/>, then 
            /// the method query the change/worker tables for changes on the user's table. If any are found, the state of the watcher is
            /// transitioned to <see cref="State.ProcessingChanges"/> and the user's function is executed with the found changes. 
            /// If execution is successful, the leases on "_rows" are released and the state transitions to <see cref="State.CheckingForChanges"/>
            /// once more
            /// </summary>
            /// <param name="token">
            /// If the token is cancelled, the thread stops polling for changes
            /// </param>
            private async Task CheckForChangesAsync(CancellationToken token)
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (_state == State.CheckingForChanges)
                        {
                            // What should we do if this call gets stuck?
                            await CheckForChangesAsync();

                            if (_rows.Count > 0)
                            {
                                _state = State.ProcessingChanges;
                                IEnumerable<SqlChangeTrackingEntry<T>> entries = null;

                                try
                                {
                                    // What should we do if this fails? It doesn't make sense to retry since it's not a connection based thing
                                    // We could still try to trigger on the correctly processed entries, but that adds additional complication because
                                    // we don't want to release the leases on the incorrectly processed entries
                                    // For now, just give up I guess?
                                    entries = GetSqlChangeTrackingEntries();
                                }
                                catch (Exception e)
                                {
                                    await ClearRows($"Failed to extract user table data from table {_userTable} associated with change metadata due to error: {e.Message}", true);
                                }

                                if (entries != null)
                                {
                                    // Should we cancel executing the function if StopAsync is called, or let it finish?
                                    // In other words, should _cancellationTokenSourceCheckingForChanges and _cancellationTokenSourceExecutor
                                    // be one token source?
                                    FunctionResult result = await _executor.TryExecuteAsync(new TriggeredFunctionData() { TriggerValue = entries },
                                        _cancellationTokenSourceExecutor.Token);
                                    if (result.Succeeded)
                                    {
                                        await ReleaseLeasesAsync();
                                    }
                                    else
                                    {
                                        // In the future might make sense to retry executing the function, but for now we just let another worker try
                                        await ClearRows($"Failed to trigger user's function for table {_userTable} due to error: {result.Exception.Message}", true);
                                    }
                                }
                            }
                        }
                        // The Delay will exit if the token is cancelled
                        await Task.Delay(SqlTriggerConstants.PollingInterval * 1000, token);
                    }
                }
                catch (Exception e)
                {
                    // Only want to log the exception if it wasn't caused by StopAsync being called, since Task.Delay throws an exception
                    // if it's cancelled
                    if (e.GetType() != typeof(TaskCanceledException))
                    {
                        _logger.LogError(e.Message);
                    }
                }
                finally
                {
                    // If this thread exits due to any reason, then the lease renewal thread should exit as well. Otherwise, it will keep looping
                    // perpetually. 
                    _cancellationTokenSourceRenewLeases.Cancel();
                    _cancellationTokenSourceCheckForChanges.Dispose();
                    _cancellationTokenSourceExecutor.Dispose();
                }
            }

            /// <summary>
            /// Queries the change/worker tables to check for new changes on the user's table. If any are found,
            /// stores the change along with the corresponding data from the user table in "_rows"
            /// </summary>
            /// <returns></returns>
            private async Task CheckForChangesAsync()
            {
                try
                {
                    using (var connection = new SqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();
                        using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
                        {
                            // Update the version number stored in the global state table if necessary before using it 
                            using (SqlCommand updateGlobalStateTableCommand = BuildUpdateGlobalStateTableCommand(connection, transaction, _userTable, 
                                _workerTable, _globalStateTable))
                            {
                                await updateGlobalStateTableCommand.ExecuteNonQueryAsync();
                            }

                            // Use the version number to query for new changes
                            using (SqlCommand getChangesCommand = BuildCheckForChangesCommand(connection, transaction))
                            {
                                using (SqlDataReader reader = await getChangesCommand.ExecuteReaderAsync())
                                {
                                    var cols = new List<string>();
                                    while (await reader.ReadAsync())
                                    {
                                        _rows.Add(SqlBindingUtilities.BuildDictionaryFromSqlRow(reader, cols));
                                    }
                                }
                            }

                            // If changes were found, acquire leases on them
                            if (_rows.Count != 0)
                            {
                                using (SqlCommand acquireLeaseCommand = BuildAcquireLeasesCommand(connection, transaction))
                                {
                                    await acquireLeaseCommand.ExecuteNonQueryAsync();
                                }
                            }
                            await transaction.CommitAsync();
                        }
                    }
                }
                catch (Exception e)
                {
                    // If there's an exception in any part of the process, we want to clear all of our data in memory and retry
                    // checking for changes again
                    _rows.Clear();
                    _whereChecks.Clear();
                    _logger.LogWarning($"Failed to check {_userTable} for new changes due to error: {e.Message}");
                }
            }

            /// <summary>
            /// Renews the leases held on _rows
            /// </summary>
            /// <returns></returns>
            private async Task RenewLeasesAsync()
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand renewLeaseCommand = BuildRenewLeasesCommand(connection))
                    {
                        await renewLeaseCommand.ExecuteNonQueryAsync();
                    }
                }
            }

            /// <summary>
            /// Resets the in-memory state of the watcher and sets it to start polling for changes again.
            /// </summary>
            /// <param name="error">
            /// The error messages the logger will report describing the reason function execution failed (used only in the case of a failure)
            /// </param>
            /// <param name="acquireLock">
            /// True if ClearRows should acquire the _rowsLock (only true in the case of a failure)
            /// </param>
            /// <returns></returns>
            private async Task ClearRows(string error, bool acquireLock)
            {
                if (acquireLock)
                {
                    _logger.LogError(error);
                    await _rowsLock.WaitAsync();
                }
                _leaseRenewalCount = 0;
                _rows.Clear();
                _whereChecks.Clear();
                _state = State.CheckingForChanges;
                _rowsLock.Release();
            }

            /// <summary>
            /// Releases the leases held on _rows
            /// </summary>
            /// <returns></returns>
            private async Task ReleaseLeasesAsync()
            {
                // Don't want to change the _rows while another thread is attempting to renew leases on them
                await _rowsLock.WaitAsync();
                long newVersionNumber = CalculateNewVersionNumber();
                try
                {
                    using (var connection = new SqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();
                        // Release the leases held on _rows
                        using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
                        {
                            using (SqlCommand releaseLeaseCommand = BuildReleaseLeasesCommand(connection, transaction))
                            {
                                await releaseLeaseCommand.ExecuteNonQueryAsync();
                            }
                            await transaction.CommitAsync();
                        }

                        // Need a separate transaction for this because need the leases the worker held on its rows released for the update
                        // version number command to recognize that all rows with VersionNumber <= newVersionNumber have been successfully processed
                        // Update the GlobalVersionNumber if possible and clean worker table
                        using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
                        {
                            using (SqlCommand updateGlobalVersionNumberCommand = BuildUpdateGlobalVersionNumberCommand(connection, transaction, newVersionNumber))
                            {
                                await updateGlobalVersionNumberCommand.ExecuteNonQueryAsync();
                            }
                            await transaction.CommitAsync();
                        }
                    }

                }
                catch (Exception e)
                {
                    // What should we do if releasing the leases fails? We could try to release them again or just wait,
                    // since eventually the lease time will expire. Then another thread will re-process the same changes though,
                    // so less than ideal. But for now that's the functionality
                    _logger.LogError($"Failed to release leases for user table {_userTable} due to error: {e.Message}");
                }
                finally
                {
                    // Want to do this before releasing the lock in case the renew leases thread wakes up. It will see that
                    // the state is checking for changes and not renew the (just released) leases
                    await ClearRows(string.Empty, false);
                }
            }

            private long CalculateNewVersionNumber()
            {
                var versionNumbers = new SortedSet<long>();
                foreach (var row in _rows)
                {
                    row.TryGetValue("SYS_CHANGE_VERSION", out string versionNumberString);
                    versionNumbers.Add(long.Parse(versionNumberString));
                }

                // If there are at least two version numbers in this set, return the second highest one
                if (versionNumbers.Count > 1)
                {
                    return versionNumbers.ElementAt(versionNumbers.Count - 2);
                }
                // Otherwise, return the only version number in the set
                else
                {
                    return versionNumbers.ElementAt(0);
                }
            }

            /// <summary>
            /// Builds the query to check for changes on the user's table (<see cref="CheckForChangesAsync"/>)
            /// </summary>
            /// <param name="connection">The connection to add to the returned SqlCommand</param>
            /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildCheckForChangesCommand(SqlConnection connection, SqlTransaction transaction)
            {
                string primaryKeysSelectList;
                string leftOuterJoinWorkerTable;
                string leftOuterJoinUserTable;
                // If one isn't in the map, none of them are
                if (!_queryStrings.TryGetValue(SqlTriggerConstants.UserTableColumnsSelectList, out string userTableColumnsSelectList))
                {
                    var nonPrimaryKeyCols = new List<string>();
                    foreach (var col in _userTableColumns)
                    {
                        if (!_primaryKeys.ContainsKey(col))
                        {
                            nonPrimaryKeyCols.Add(col);
                        }
                    }

                    // Every column in the user table is a primary key, in which case this list should be empty
                    if (nonPrimaryKeyCols.Count == 0)
                    {
                        userTableColumnsSelectList = string.Empty;
                    }
                    else
                    {
                        userTableColumnsSelectList = string.Join(", ", nonPrimaryKeyCols.Select(col => $"u.{col}")) + ", ";
                    }

                    primaryKeysSelectList = string.Join(", ", _primaryKeys.Keys.Select(key => $"c.{key}"));
                    leftOuterJoinWorkerTable = string.Join(" AND ", _primaryKeys.Keys.Select(key => $"c.{key} = w.{key}"));
                    leftOuterJoinUserTable = string.Join(" AND ", _primaryKeys.Keys.Select(key => $"c.{key} = u.{key}"));

                    _queryStrings.Add(SqlTriggerConstants.PrimaryKeysSelectList, primaryKeysSelectList);
                    _queryStrings.Add(SqlTriggerConstants.UserTableColumnsSelectList, userTableColumnsSelectList);
                    _queryStrings.Add(SqlTriggerConstants.LeftOuterJoinWorkerTable, leftOuterJoinWorkerTable);
                    _queryStrings.Add(SqlTriggerConstants.LeftOuterJoinUserTable, leftOuterJoinUserTable);
                }
                else
                {
                    _queryStrings.TryGetValue(SqlTriggerConstants.PrimaryKeysSelectList, out primaryKeysSelectList);
                    _queryStrings.TryGetValue(SqlTriggerConstants.LeftOuterJoinWorkerTable, out leftOuterJoinWorkerTable);
                    _queryStrings.TryGetValue(SqlTriggerConstants.LeftOuterJoinUserTable, out leftOuterJoinUserTable);
                }

                var getChangesQuery =
                    $"DECLARE @version bigint;\n" +
                    $"SELECT @version = GlobalVersionNumber FROM {_globalStateTable} WHERE UserTableID = OBJECT_ID(\'{_userTable}\');\n" +
                    $"SELECT TOP {SqlTriggerConstants.BatchSize} *\n" +
                    $"FROM\n" +
                    $"(SELECT {primaryKeysSelectList}, {userTableColumnsSelectList}c.SYS_CHANGE_VERSION, c.SYS_CHANGE_CREATION_VERSION, c.SYS_CHANGE_OPERATION, \n" +
                    $"c.SYS_CHANGE_COLUMNS, c.SYS_CHANGE_CONTEXT, w.LeaseExpirationTime, w.DequeueCount, w.VersionNumber\n" +
                    $"FROM CHANGETABLE (CHANGES {_userTable}, @version) AS c\n" +
                    $"LEFT OUTER JOIN {_workerTable} AS w ON {leftOuterJoinWorkerTable}\n" +
                    $"LEFT OUTER JOIN {_userTable} AS u ON {leftOuterJoinUserTable}) AS CHANGES\n" +
                    $"WHERE (Changes.LeaseExpirationTime IS NULL AND\n" +
                    $"(Changes.VersionNumber IS NULL OR Changes.VersionNumber < Changes.SYS_CHANGE_VERSION)\n" +
                    $"OR Changes.LeaseExpirationTime < SYSDATETIME())\n" +
                    $"AND (Changes.DequeueCount IS NULL OR Changes.DequeueCount < {SqlTriggerConstants.MaxDequeueCount})\n" +
                    $"ORDER BY Changes.SYS_CHANGE_VERSION ASC;\n";

                return new SqlCommand(getChangesQuery, connection, transaction);
            }

            /// <summary>
            /// Builds the query to acquire leases on the rows in "_rows" if changes are detected in the user's table (<see cref="CheckForChangesAsync"/>)
            /// </summary>
            /// <param name="connection">The connection to add to the returned SqlCommand</param>
            /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildAcquireLeasesCommand(SqlConnection connection, SqlTransaction transaction)
            {
                var acquireLeasesCommand = new SqlCommand();
                SqlBindingUtilities.AddPrimaryKeyParametersToCommand(acquireLeasesCommand, _rows, _primaryKeys.Keys);
                var acquireLeaseOnRows = string.Empty;
                var index = 0;

                foreach (var row in _rows)
                {
                    var whereCheck = string.Join(" AND ", _primaryKeys.Keys.Select(key => $"{key} = @{key}_{index}"));
                    var valuesList = string.Join(", ", _primaryKeys.Keys.Select(key => $"@{key}_{index}"));
                    _whereChecks.Add(whereCheck);

                    row.TryGetValue("SYS_CHANGE_VERSION", out string versionNumber);
                    acquireLeaseOnRows +=
                        $"IF NOT EXISTS (SELECT * FROM {_workerTable} WHERE {whereCheck})\n" +
                        $"INSERT INTO {_workerTable}\n" +
                        $"VALUES ({valuesList}, DATEADD({SqlTriggerConstants.LeaseUnits}, {SqlTriggerConstants.LeaseTime}, SYSDATETIME()), 0, {versionNumber})\n" +
                        $"ELSE\n" +
                        $"UPDATE {_workerTable}\n" +
                        $"SET LeaseExpirationTime = DATEADD({SqlTriggerConstants.LeaseUnits}, {SqlTriggerConstants.LeaseTime}, SYSDATETIME()), DequeueCount = DequeueCount + 1, " +
                        $"VersionNumber = {versionNumber}\n" +
                        $"WHERE {whereCheck};\n";

                    index++;
                }

                acquireLeasesCommand.CommandText = acquireLeaseOnRows;
                acquireLeasesCommand.Connection = connection;
                acquireLeasesCommand.Transaction = transaction;
                return acquireLeasesCommand;
            }

            /// <summary>
            /// Builds the query to renew leases on the rows in "_rows" (<see cref="RenewLeasesAsync(CancellationToken)"/>)
            /// </summary>
            /// <param name="connection">The connection to add to the returned SqlCommand</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildRenewLeasesCommand(SqlConnection connection)
            {
                SqlCommand renewLeasesCommand = new SqlCommand();
                SqlBindingUtilities.AddPrimaryKeyParametersToCommand(renewLeasesCommand, _rows, _primaryKeys.Keys);
                var renewLeaseOnRows = string.Empty;
                var index = 0;

                foreach (var row in _rows)
                {
                    renewLeaseOnRows +=
                    $"UPDATE {_workerTable}\n" +
                    $"SET LeaseExpirationTime = DATEADD({SqlTriggerConstants.LeaseUnits}, {SqlTriggerConstants.LeaseTime}, SYSDATETIME())\n" +
                    $"WHERE {_whereChecks.ElementAt(index++)};\n";
                }

                renewLeasesCommand.CommandText = renewLeaseOnRows;
                renewLeasesCommand.Connection = connection;

                return renewLeasesCommand;
            }

            /// <summary>
            /// Builds the query to release leases on the rows in "_rows" after successful invocation of the user's function (<see cref="CheckForChangesAsync"/>)
            /// </summary>
            /// <param name="connection">The connection to add to the returned SqlCommand</param>
            /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildReleaseLeasesCommand(SqlConnection connection, SqlTransaction transaction)
            {
                SqlCommand releaseLeasesCommand = new SqlCommand();
                var releaseLeasesOnRows = $"DECLARE @current_version bigint;\n";
                SqlBindingUtilities.AddPrimaryKeyParametersToCommand(releaseLeasesCommand, _rows, _primaryKeys.Keys);
                var index = 0;

                foreach (var row in _rows)
                {
                    var whereCheck = _whereChecks.ElementAt(index++);
                    row.TryGetValue("SYS_CHANGE_VERSION", out string versionNumber);

                    releaseLeasesOnRows +=
                        $"SELECT @current_version = VersionNumber\n" +
                        $"FROM {_workerTable}\n" +
                        $"WHERE {whereCheck};\n" +
                        $"IF {versionNumber} >= @current_version\n" +
                        $"UPDATE {_workerTable}\n" +
                        $"SET LeaseExpirationTime = NULL, DequeueCount = 0, VersionNumber = {versionNumber}\n" +
                        $"WHERE {whereCheck};\n";
                }

                releaseLeasesCommand.CommandText = releaseLeasesOnRows;
                releaseLeasesCommand.Connection = connection;
                releaseLeasesCommand.Transaction = transaction;

                return releaseLeasesCommand;
            }

            /// <summary>
            /// Builds the query to update the global version number in _globalStateTable after successful invocation of the user's function (<see cref="CheckForChangesAsync"/>)
            /// </summary>
            /// <param name="connection">The connection to add to the returned SqlCommand</param>
            /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
            /// <param name="newVersionNumber">The new GlobalVersionNumber to store in the _globalStateTable for this _userTable</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildUpdateGlobalVersionNumberCommand(SqlConnection connection, SqlTransaction transaction, long newVersionNumber)
            {
                _queryStrings.TryGetValue(SqlTriggerConstants.LeftOuterJoinWorkerTable, out string leftOuterJoin);
                var updateVersionNumber =
                    $"DECLARE @current_version bigint;\n" +
                    $"DECLARE @new_version bigint;\n" +
                    $"DECLARE @unprocessed_changes bigint;\n" +
                    $"SELECT @current_version = GlobalVersionNumber FROM {_globalStateTable} WHERE UserTableID = OBJECT_ID(\'{_userTable}\');\n" +
                    $"SET @new_version = {newVersionNumber};\n" +
                    $"SELECT @unprocessed_changes = \n" +
                    $"COUNT(*)\n" +
                    $"FROM\n" +
                    $"(SELECT c.SYS_CHANGE_VERSION FROM CHANGETABLE(CHANGES {_userTable}, @current_version) AS c\n" +
                    $"LEFT OUTER JOIN {_workerTable} AS w ON {leftOuterJoin}\n" +
                    $"WHERE c.SYS_CHANGE_VERSION <= @new_version\n" +
                    $"AND ((w.VersionNumber IS NULL OR w.VersionNumber != c.SYS_CHANGE_VERSION OR w.LeaseExpirationTime IS NOT NULL)\n" +
                    $"AND (w.DequeueCount IS NULL OR w.DequeueCount < {SqlTriggerConstants.MaxDequeueCount}))) AS Changes;\n" +
                    $"IF @unprocessed_changes = 0 AND @new_version > @current_version\n" +
                    $"BEGIN\n" +
                    $"UPDATE {_globalStateTable}\n" +
                    $"SET GlobalVersionNumber = @new_version WHERE UserTableID = OBJECT_ID(\'{_userTable}\');\n" +
                    $"DELETE FROM {_workerTable}\n" +
                    $"WHERE VersionNumber <= {newVersionNumber};\n" +
                    $"END";

                return new SqlCommand(updateVersionNumber, connection, transaction);
            }

            /// <summary>
            /// Builds up the list of SqlChangeTrackingEntries passed to the user's triggered function based on the data
            /// stored in "_rows"
            /// If any of the entries correspond to a deleted row, then the <see cref="SqlChangeTrackingEntry.Data"> will be populated
            /// with only the primary key values of the deleted row.
            /// </summary>
            /// <returns>The list of entries</returns>
            private IEnumerable<SqlChangeTrackingEntry<T>> GetSqlChangeTrackingEntries()
            {
                var entries = new List<SqlChangeTrackingEntry<T>>();
                foreach (var row in _rows)
                {
                    SqlChangeType changeType = GetChangeType(row);
                    // If the row has been deleted, there is no longer any data for it in the user table. The best we can do
                    // is populate the entry with the primary key values of the row
                    if (changeType == SqlChangeType.Deleted)
                    {
                        entries.Add(new SqlChangeTrackingEntry<T>(changeType, JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(BuildDefaultDictionary(row)))));
                    }
                    else
                    {
                        var userTableRow = new Dictionary<string, string>();
                        foreach (var col in _userTableColumns)
                        {
                            row.TryGetValue(col, out string colVal);
                            userTableRow.Add(col, colVal);
                        }
                        entries.Add(new SqlChangeTrackingEntry<T>(GetChangeType(row), JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(userTableRow))));
                    }
                }
                return entries;
            }

            /// <summary>
            /// Gets the change associated with this row (either an insert, update or delete)
            /// </summary>
            /// <param name="row">
            /// The (combined) row from the change table and worker table
            /// </param>
            /// <exception cref="ArgumentException">
            /// Thrown if "row" does not contain the column "SYS_CHANGE_OPERATION"
            /// </exception>
            /// <exception cref="InvalidDataException">
            /// Thrown if the value of the "SYS_CHANGE_OPERATION" column is none of "I", "U", or "D"
            /// </exception>
            /// <returns>
            /// SqlChangeType.Created for an insert, SqlChangeType.Changed for an update,
            /// and SqlChangeType.Deleted for a delete 
            /// </returns>
            private static SqlChangeType GetChangeType(Dictionary<string, string> row)
            {
                if (!row.TryGetValue("SYS_CHANGE_OPERATION", out string changeType))
                {
                    throw new ArgumentException($"Row does not contain the column SYS_CHANGE_OPERATION from SQL's change table: {row}");
                }
                if (changeType.Equals("I"))
                {
                    return SqlChangeType.Inserted;
                }
                else if (changeType.Equals("U"))
                {
                    return SqlChangeType.Updated;
                }
                else if (changeType.Equals("D"))
                {
                    return SqlChangeType.Deleted;
                }
                else
                {
                    throw new InvalidDataException($"Invalid change type encountered in change table row: {row}");
                }
            }

            /// <summary>
            /// Builds up a default POCO in which only the fields corresponding to the primary keys are populated
            /// </summary>
            /// <param name="row">
            /// Contains the values of the primary keys that the POCO is populated with
            /// </param>
            /// <returns>The default POCO</returns>
            private Dictionary<string, string> BuildDefaultDictionary(Dictionary<string, string> row)
            {
                var defaultDictionary = new Dictionary<string, string>();
                foreach (var primaryKey in _primaryKeys.Keys)
                {
                    row.TryGetValue(primaryKey, out string primaryKeyValue);
                    defaultDictionary.Add(primaryKey, primaryKeyValue);
                }
                return defaultDictionary;
            }

            enum State
            {
                CheckingForChanges,
                ProcessingChanges
            }
        }

        /// <summary>
        /// Creates the worker table associated with the user's table, if one does not already exist
        /// </summary>
        /// <param name="connectionString">The SQL connection string used to connect to the user database</param>
        /// <param name="primaryKeys">An empty map from primary key name to primary key type that will be populated</param>
        /// <param name="userTableColumns">An empty list of user table column names that will be populated</param>
        /// <param name="userTable">The (sanitized) name of the user table</param>
        /// <param name="globalStateTable">The (sanitized) name of the global state table</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if any part of the process failed, so the worker table name was not generated correctly
        /// </exception>
        /// <returns>
        /// The (sanitized) name of the worker table, which follows the format [az_func].[Worker_Table_UserTableID], where
        /// UserTableID is the result of a call to OBJECT_ID('userTable')
        /// </returns>
        private static async Task<string> CreateWorkerTablesAsync(
            string connectionString, 
            Dictionary<string, string> primaryKeys,
            List<string> userTableColumns,
            string userTable,
            string globalStateTable,
            ILogger logger)
        {
            string workerTable = null;
            // Do I need a transaction for this?
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                int userTableID = await GetUserTableSchemaAsync(connectionString, userTable, primaryKeys, userTableColumns);
                workerTable = "[az_func].[Worker_Table_" + userTableID + "]";

                // Create the global state table, if one doesn't already exist for this database
                using (SqlCommand createGlobalStateTableCommand = BuildCreateGlobalStateTableCommandAsync(connection, globalStateTable))
                {
                    await createGlobalStateTableCommand.ExecuteNonQueryAsync();
                }
                // Insert a row into the global state table for this user table, if one doesn't already exist
                using (SqlCommand insertRowGlobalStateTableCommand = BuildInsertRowGlobalStateTableCommandAsync(connection, globalStateTable, userTable))
                {
                    try
                    {
                        await insertRowGlobalStateTableCommand.ExecuteNonQueryAsync();
                    }
                    // Could fail if we try to insert a NULL value into the GlobalVersionNumber, which happens when CHANGE_TRACKING_MIN_VALID_VERSION 
                    // returns NULL for the user table, meaning that change tracking is not enabled for either the database or table (or both)
                    catch (Exception e)
                    {
                        var errorMessage = $"Failed to start processing changes to table {userTable}, potentially because change tracking was not " +
                            $"enabled for the table or database {connection.Database}.";
                        logger.LogWarning(errorMessage + $" Exact exception thrown is {e.Message}");
                        throw new InvalidOperationException(errorMessage);
                    }
                }
                // Create the worker table, if one doesn't already exist for this user table
                using (SqlCommand createWorkerTableCommand = BuildCreateWorkerTableCommand(connection, primaryKeys, workerTable))
                {
                    await createWorkerTableCommand.ExecuteNonQueryAsync();
                }
            }

            if (workerTable == null)
            {
                throw new Exception($"Failed to generate a worker table name for user table {userTable}");
            }
            return workerTable;
        }

        /// <summary>
        /// Builds the command to create the worker table if one does not already exist (<see cref="CreateWorkerTablesAsync"/>)
        /// </summary>
        /// <param name="connection">The connection to attach to the returned SqlCommand</param>
        /// <param name="primaryKeys">An empty map from primary key name to primary key type that will be populated</param>
        /// <param name="workerTable">The (sanitized) name of the worker table</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private static SqlCommand BuildCreateWorkerTableCommand(
            SqlConnection connection,
            Dictionary<string, string> primaryKeys,
            string workerTable)
        {

            string primaryKeysWithTypes = string.Join(",\n", primaryKeys.Select(pair => $"{pair.Key} {pair.Value}"));
            string primaryKeysList = string.Join(", ", primaryKeys.Keys);

            var createTableString =
                $"IF OBJECT_ID(N\'{workerTable}\', \'U\') IS NULL\n" +
                $"CREATE TABLE {workerTable} (\n" +
                $"{primaryKeysWithTypes},\n" +
                $"LeaseExpirationTime datetime2,\n" +
                $"DequeueCount int,\n" +
                $"VersionNumber bigint\n" +
                $"PRIMARY KEY({primaryKeysList})\n" +
                $");\n";

            return new SqlCommand(createTableString, connection);
        }

        /// <summary>
        /// Builds the command to create the global state table if one does not already exist (<see cref="CreateWorkerTablesAsync"/>)
        /// </summary>
        /// <param name="connection">The connection to attach to the returned SqlCommand</param>
        /// <param name="globalStateTable">The (sanitized) name of the global state table</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private static SqlCommand BuildCreateGlobalStateTableCommandAsync(
            SqlConnection connection,
            string globalStateTable)
        {
            // Maximum number of characters in a table name and schema name is 128 each, so together that's around 300
            var createTableString =
                $"IF OBJECT_ID(N\'{globalStateTable}\', \'U\') IS NULL\n" +
                $"CREATE TABLE {globalStateTable} (\n" +
                $"UserTableID int PRIMARY KEY,\n" +
                $"GlobalVersionNumber bigint NOT NULL,\n" +
                $"DatabaseID int NOT NULL\n" +
                $");";
            return new SqlCommand(createTableString, connection);
        }

        /// <summary>
        /// Builds the command to insert a row into the global state table for this user table, if such a row doesn't already exist
        /// </summary>
        /// <param name="connection">The connection to attach to the returned SqlCommand</param>
        /// <param name="userTable">The (sanitized) name of the user table</param>
        /// <param name="globalStateTable">The (sanitized) name of the global state table</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private static SqlCommand BuildInsertRowGlobalStateTableCommandAsync(
            SqlConnection connection,
            string globalStateTable,
            string userTable)
        {
            var insertRowString =
                $"IF NOT EXISTS (SELECT * FROM {globalStateTable} WHERE UserTableID = OBJECT_ID(\'{userTable}\'))\n" +
                $"INSERT INTO {globalStateTable}\n" +
                $"VALUES (OBJECT_ID(\'{userTable}\'), CHANGE_TRACKING_MIN_VALID_VERSION(OBJECT_ID(\'{userTable}\')), DB_ID());\n";

            return new SqlCommand(insertRowString, connection);
        }

        /// <summary>
        /// Builds the command to update the global state table in the case of data loss or a new minimum valid version number
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <param name="userTable">The (sanitized) name of the user table</param>
        /// <param name="workerTable">The (sanitized) name of the worker table</param>
        /// <param name="globalStateTable">The (sanitized) name of the global state table</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private static SqlCommand BuildUpdateGlobalStateTableCommand(
            SqlConnection connection, 
            SqlTransaction transaction, 
            string userTable,
            string workerTable,
            string globalStateTable)
        {
            var updateGlobalStateTable =
                $"DECLARE @min_version bigint;\n" +
                $"DECLARE @current_version bigint;\n" +
                $"DECLARE @db_id int;\n" +
                $"DECLARE @user_table_id int;\n" +
                $"SET @user_table_id = OBJECT_ID(\'{userTable}\')\n" +
                $"SET @min_version = CHANGE_TRACKING_MIN_VALID_VERSION(@user_table_id);\n" +
                $"SELECT @current_version = GlobalVersionNumber FROM {globalStateTable} WHERE UserTableID = @user_table_id;\n" +
                $"SELECT @db_id = DatabaseID FROM {globalStateTable} WHERE UserTableID = @user_table_id;\n" +
                $"IF @db_id != DB_ID()\n" +
                $"TRUNCATE TABLE {workerTable};\n" +
                $"IF @current_version < @min_version OR @db_id != DB_ID()\n" +
                $"UPDATE {globalStateTable}\n" +
                $"SET GlobalVersionNumber = @min_version, DatabaseID = DB_ID()\n" +
                $"WHERE UserTableID = @user_table_id;";

            return new SqlCommand(updateGlobalStateTable, connection, transaction);
        }

        /// <summary>
        /// Retrieves the primary keys of the user's table and stores them in the "_primaryKeys" dictionary,
        /// which maps from primary key name to primary key type
        /// Also retrieves the column names of the user's table and stores them in "_userTableColumns", as well 
        /// as the user table's OBJECT_ID which it returns as an int
        /// </summary>
        /// <param name="connectionString">The SQL connection string used to connect to the user database</param>
        /// <param name="primaryKeys">An empty map from primary key name to primary key type that will be populated</param>
        /// <param name="userTableColumns">An empty list of user table column names that will be populated</param>
        /// <param name="userTable">The (sanitized) name of the user table</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the query to retrieve the OBJECT_ID of the user table fails to correctly execute
        /// This can happen if the OBJECT_ID call returns NULL, meaning that the user table might not exist in the database
        /// </exception>
        /// <returns>The OBJECT_ID of userTable</returns>
        private static async Task<int> GetUserTableSchemaAsync(
            string connectionString,
            string userTable,
            Dictionary<string, string> primaryKeys,
            List<string> userTableColumns)
        {
            var getPrimaryKeysQuery =
                $"SELECT c.name, t.name, c.max_length, c.precision, c.scale\n" +
                $"FROM sys.indexes i\n" +
                $"INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id\n" +
                $"INNER JOIN sys.columns c ON ic.object_id = c.object_id AND c.column_id = ic.column_id\n" +
                $"INNER JOIN sys.types t ON c.user_type_id = t.user_type_id\n" +
                $"WHERE i.is_primary_key = 1 and i.object_id = OBJECT_ID(\'{userTable}\');";

            var getColumnNamesQuery =
                $"SELECT name\n" +
                $"FROM sys.columns\n" +
                $"WHERE object_id = OBJECT_ID(\'{userTable}\');";

            var getObjectIDQuery =
                $"SELECT OBJECT_ID(\'{userTable}\');";

            // Necessary in the case that a prior attempt to start the SqlTableWatcher failed.
            // Could be the case that these were partially populated, so should clear and repopulate them
            primaryKeys.Clear();

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                // Determine the primary keys of the user table
                using (var getPrimaryKeysCommand = new SqlCommand(getPrimaryKeysQuery, connection))
                {
                    using (SqlDataReader reader = await getPrimaryKeysCommand.ExecuteReaderAsync())
                    {
                        await DeterminePrimaryKeyTypes(reader, primaryKeys);
                    }
                }
                
                // Determine the user table column names, if necessary
                if (userTableColumns != null)
                {
                    userTableColumns.Clear();
                    using (var getColumnNamesCommand = new SqlCommand(getColumnNamesQuery, connection))
                    {
                        using (SqlDataReader reader = await getColumnNamesCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                userTableColumns.Add(reader.GetString(0));
                            }
                        }
                    }
                }
                
                // Determine the OBJECT_ID of the user table and return it
                using (var getObjectIDCommand = new SqlCommand(getObjectIDQuery, connection))
                {
                    using (SqlDataReader reader = await getObjectIDCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var userTableID = reader.GetValue(0);
                            // Call to OBJECT_ID returned null
                            if (userTableID is DBNull)
                            {
                                throw new InvalidOperationException($"Failed to determine the OBJECT_ID of the user table {userTable}. " +
                                    $"Possibly {userTable} does not exist in the database.");
                            }
                            else
                            {
                                return (int)userTableID;
                            }
                        }
                    }
                }
            }
            throw new InvalidOperationException($"Failed to determine the OBJECT_ID of the user table {userTable}");
        }

        /// <summary>
        /// Adds the primary key name (first column returned by the reader) and type to the primaryKeys dictionary.
        /// Adds length arguments if the type of any of those listed in variableLengthTypes, and precision and 
        /// scale arguments if it is any of those listed in variablePrecisionTypes
        /// Otherwise, if the type accepts arguments (like datetime2), just uses the default which is the highest
        /// precision for all other types
        /// </summary>
        /// <param name="reader">Contains each primary key name and corresponding type information</param>
        /// <param name="primaryKeys">The (empty) dictionary to populate</param>
        private static async Task DeterminePrimaryKeyTypes(SqlDataReader reader, Dictionary<string, string> primaryKeys)
        {
            while (await reader.ReadAsync())
            {
                var type = reader.GetString(1);
                if (variableLengthTypes.Contains(type))
                {
                    var length = reader.GetInt16(2);
                    // Special "max" case. I'm actually not sure it's valid to have varchar(max) as a primary key because
                    // it exceeds the byte limit of an index field (900 bytes), but just in case
                    if (length == -1)
                    {
                        type += "(max)";
                    }
                    else
                    {
                        type += "(" + length + ")";
                    }
                }
                else if (variablePrecisionTypes.Contains(type))
                {
                    int precision = (int)reader.GetByte(3);
                    int scale = (int)reader.GetByte(4);
                    type += "(" + precision + "," + scale + ")";
                }
                primaryKeys.Add(reader.GetString(0), type);
            }
        }
    }
}