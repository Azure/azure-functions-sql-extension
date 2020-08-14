// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
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
    internal class SqlTableWatcher<T>
    {
        private readonly string _workerTable;
        private readonly string _userTable;
        private readonly string _connectionString;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ILogger _logger;
        private CancellationTokenSource _cancellationTokenSourceExecutor;
        private readonly CancellationTokenSource _cancellationTokenSourceCheckForChanges;
        private readonly CancellationTokenSource _cancellationTokenSourceRenewLeases;

        // It should be impossible for multiple threads to access these at the same time because of the semaphores we use
        private readonly List<Dictionary<string, string>> _rows;
        private readonly List<string> _userTableColumns;
        private readonly List<string> _whereChecks;
        private readonly Dictionary<string, string> _primaryKeys;
        private readonly Dictionary<string, string> _queryStrings;

        private readonly SemaphoreSlim _rowsLock;
        private State _state;
        private int _leaseRenewalCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTableWatcher<typeparamref name="T"/>> class
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
        /// Thrown if the executor is null
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if table or connectionString are null or empty
        /// </exception>
        public SqlTableWatcher(string table, string connectionString, ITriggeredFunctionExecutor executor, ILogger logger)
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
            _workerTable = SqlBindingUtilities.NormalizeTableName(BuildWorkerTableName(table));

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
            await CreateWorkerTableAsync();
            Task.Run(() =>
            {
                CheckForChangesAsync(_cancellationTokenSourceCheckForChanges.Token);
                RenewLeasesAsync(_cancellationTokenSourceRenewLeases.Token);
            });
        }

        /// <summary>
        /// Stops the watcher which stops polling for changes on the user's table.
        /// If the watcher is currently executing a set of changes, it is only stopped
        /// once execution is finished and the user's function is triggered (whether or not
        /// the trigger is successful) 
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync()
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
                    // Could have been cancelled by ClearRows while it had the _rowsLock
                    if (!token.IsCancellationRequested)
                    {
                        try
                        {
                            if (_state == State.ProcessingChanges && _leaseRenewalCount < SqlTriggerConstants.MaxLeaseRenewalCount)
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
                                if (_leaseRenewalCount == SqlTriggerConstants.MaxLeaseRenewalCount)
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
                                await ClearRows($"Failed to extract user table data from table {_userTable} associated with change metadata due to error: {e.Message}");
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
                                    await ClearRows($"Failed to trigger user's function for table {_userTable} due to error: {result.Exception.Message}");
                                }
                            }

                            if (token.IsCancellationRequested)
                            {
                                // Might as well skip delaying the task and immediately break out of the while loop
                                break;
                            }
                        }
                        await Task.Delay(SqlTriggerConstants.PollingInterval * 1000, token);
                    }
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
                // perpetually. Though could have been already cancelled by ClearRows
                if (!_cancellationTokenSourceRenewLeases.IsCancellationRequested)
                {
                    _cancellationTokenSourceRenewLeases.Cancel();
                    _cancellationTokenSourceExecutor.Dispose();
                    _cancellationTokenSourceCheckForChanges.Dispose();
                }
            }
        }

        /// <summary>
        /// Creates the worker table associated with the user's table, if one does not already exist
        /// </summary>
        /// <returns></returns>
        private async Task CreateWorkerTableAsync()
        {

            // Should maybe change this so that we don't have to extract the connection string from the app setting
            // every time
            using (var connection = new SqlConnection(_connectionString))
            {
                using (SqlCommand createTableCommand = await BuildCreateTableCommandAsync(connection))
                {
                    await connection.OpenAsync();
                    await createTableCommand.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Retrieves the primary keys of the user's table and stores them in the "_primaryKeys" dictionary,
        /// which maps from primary key name to primary key type
        /// Also retrieves the column names of the user's table and stores them in "_userTableColumns"
        /// </summary>
        /// <returns></returns>
        private async Task GetUserTableSchemaAsync()
        {
            var getPrimaryKeysQuery =
                $"SELECT c.name, t.name\n" +
                $"FROM sys.indexes i\n" +
                $"INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id\n" +
                $"INNER JOIN sys.columns c ON ic.object_id = c.object_id AND c.column_id = ic.column_id\n" +
                $"INNER JOIN sys.types t ON c.user_type_id = t.user_type_id\n" +
                $"WHERE i.is_primary_key = 1 and i.object_id = OBJECT_ID(\'{_userTable}\');";

            var getColumnNamesQuery =
                $"SELECT name\n" +
                $"FROM sys.columns\n" +
                $"WHERE object_id = OBJECT_ID(\'{_userTable}\');";

            using (var connection = new SqlConnection(_connectionString))
            {
                using (var getPrimaryKeysCommand = new SqlCommand(getPrimaryKeysQuery, connection))
                {
                    await connection.OpenAsync();
                    using (SqlDataReader reader = await getPrimaryKeysCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _primaryKeys.Add(reader.GetString(0), reader.GetString(1));
                        }
                    }
                }
                using (var getColumnNamesCommand = new SqlCommand(getColumnNamesQuery, connection))
                {
                    using (SqlDataReader reader = await getColumnNamesCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _userTableColumns.Add(reader.GetString(0));
                        }
                    }
                }
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
        /// Used in the case of some sort of failure during execution, in which case we do not release the leases
        /// since the rows were not successfully processed. Eventually, another worker will pick up these changes
        /// and try to process them. 
        /// </summary>
        /// <param name="error">
        /// The error messages the logger will report describing the reason of the failued execution
        /// </param>
        /// <returns></returns>
        private async Task ClearRows(string error)
        {
            _logger.LogError(error);
            await _rowsLock.WaitAsync();
            // No idea how this could fail, but just in case
            try
            {
                _leaseRenewalCount = 0;
                _rows.Clear();
                _whereChecks.Clear();
                _state = State.CheckingForChanges;

            }
            catch (Exception e)
            {
                // If we somehow failed to clear memory, we are in an unacceptable state. The watcher should immediately be cancelled
                _logger.LogError($"Failed to clear in-memory state due to error: {e.Message}. All threads being immediately cancelled and watcher being stopped.");
                _cancellationTokenSourceCheckForChanges.Cancel();
                _cancellationTokenSourceRenewLeases.Cancel();
            }
            finally
            {
                _rowsLock.Release();
            }
        }

        /// <summary>
        /// Releases the leases held on _rows
        /// </summary>
        /// <returns></returns>
        private async Task ReleaseLeasesAsync()
        {
            // Don't want to change the _rows while another thread is attempting to renew leases on them
            await _rowsLock.WaitAsync();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
                    {
                        using (SqlCommand releaseLeaseCommand = BuildReleaseLeasesCommand(connection, transaction))
                        {
                            await releaseLeaseCommand.ExecuteNonQueryAsync();
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
                _rows.Clear();
                _whereChecks.Clear();
                _leaseRenewalCount = 0;
                _state = State.CheckingForChanges;
                _rowsLock.Release();
            }
        }

        /// <summary>
        /// Builds the query to create the worker table if one does not already exist (<see cref="CreateWorkerTableAsync"/>)
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private async Task<SqlCommand> BuildCreateTableCommandAsync(SqlConnection connection)
        {
            await GetUserTableSchemaAsync();

            string primaryKeysWithTypes = string.Join(",\n", _primaryKeys.Select(pair => $"{pair.Key} {pair.Value}"));
            string primaryKeysList = string.Join(", ", _primaryKeys.Keys);

            var createTableString =
                $"IF OBJECT_ID(N\'{_workerTable}\', \'U\') IS NULL\n" +
                $"BEGIN\n" +
                $"CREATE TABLE {_workerTable} (\n" +
                $"{primaryKeysWithTypes},\n" +
                $"LeaseExpirationTime datetime2,\n" +
                $"DequeueCount int,\n" +
                $"VersionNumber bigint\n" +
                $"PRIMARY KEY({primaryKeysList})\n" +
                $");\n" +
                $"END";
            return new SqlCommand(createTableString, connection);
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

                userTableColumnsSelectList = string.Join(", ", nonPrimaryKeyCols.Select(col => $"u.{col}"));
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
                $"SET @version = CHANGE_TRACKING_MIN_VALID_VERSION(OBJECT_ID(\'{_userTable}\'));\n" +
                $"SELECT TOP {SqlTriggerConstants.BatchSize} *\n" +
                $"FROM\n" +
                $"(SELECT {primaryKeysSelectList}, {userTableColumnsSelectList}, c.SYS_CHANGE_VERSION, c.SYS_CHANGE_CREATION_VERSION, c.SYS_CHANGE_OPERATION, \n" +
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
        /// Returns the name of the worker table, removing the schema prefix from userTable if one exists as well as any
        /// enclosing square brackets
        /// </summary>
        /// <param name="userTable">The user table name the worker table name is based on</param>
        /// <returns>The worker table name</returns>
        private static string BuildWorkerTableName(string userTable)
        {
            string[] tableNameComponents = userTable.Split(new[] { '.' }, 2);
            string tableName;
            // Don't want the schema name of the user table in the name of the worker table. If the user table is 
            // specified with a schema prefix, the result of the Split call will have the user table name stored
            // in the second index. If the result of the Split call doesn't have two elements, then the user table
            // wasn't prefixed with a schema so we can just use it directly
            if (tableNameComponents.Length == 2)
            {
                tableName = tableNameComponents[1];
            }
            else
            {
                tableName = userTable;
            }

            // Don't want to include brackets if the user specified the table name with them
            if (tableName.StartsWith('[') && tableName.EndsWith(']'))
            {
                tableName = tableName.Substring(1, tableName.Length - 2);
            }

            return SqlTriggerConstants.Schema + ".Worker_Table_" + tableName;
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
}