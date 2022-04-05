// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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
    internal sealed class SqlTableChangeMonitor<T> : IDisposable
    {
        public const string Schema = "az_func";
        public const int BatchSize = 10;
        public const int MaxDequeueCount = 5;
        public const int MaxLeaseRenewalCount = 5;
        public const int LeaseIntervalInSeconds = 30;
        public const int PollingIntervalInSeconds = 5;

        private readonly string[] variableLengthTypes = new string[] { "varchar", "nvarchar", "nchar", "char", "binary", "varbinary" };
        private readonly string[] variablePrecisionTypes = new string[] { "numeric", "decimal" };

        private readonly string _workerId;
        private string _workerTable;
        private int _userTableId;
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

        private string _primaryKeysSelectList;
        private string _userTableColumnsSelectList;
        private string _leftOuterJoinUserTable;
        private string _leftOuterJoinWorkerTable;

        private readonly SemaphoreSlim _rowsLock;
        private State _state;
        private int _leaseRenewalCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTableChangeMonitor{T}" />> class
        /// </summary>
        /// <param name="connectionString">
        /// The SQL connection string used to connect to the user's database
        /// </param>
        /// <param name="table"> 
        /// The name of the user table that changes are being tracked on
        /// </param>
        /// <param name="workerId">
        /// The worker application ID
        /// </param>
        /// <param name="executor">
        /// Used to execute the user's function when changes are detected on "table"
        /// </param>
        /// <param name="logger">
        /// Ilogger used to log information and warnings
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the executor or logger is null
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if table or connectionString are null or empty
        /// </exception>
        public SqlTableChangeMonitor(string table, string connectionString, string workerId, ITriggeredFunctionExecutor executor, ILogger logger)
        {
            _ = !string.IsNullOrEmpty(table) ? table : throw new ArgumentNullException(nameof(table));
            this._connectionString = !string.IsNullOrEmpty(connectionString) ? connectionString : throw new ArgumentNullException(nameof(connectionString));
            this._workerId = !string.IsNullOrEmpty(workerId) ? workerId : throw new ArgumentNullException(nameof(workerId));
            this._executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));

            this._userTable = SqlBindingUtilities.NormalizeTableName(table);
            this._globalStateTable = $"[{Schema}].[Global_State_Table]";

            this._cancellationTokenSourceExecutor = new CancellationTokenSource();
            this._cancellationTokenSourceCheckForChanges = new CancellationTokenSource();
            this._cancellationTokenSourceRenewLeases = new CancellationTokenSource();
            this._rowsLock = new SemaphoreSlim(1);

            this._rows = new List<Dictionary<string, string>>();
            this._userTableColumns = new List<string>();
            this._whereChecks = new List<string>();
            this._primaryKeys = new Dictionary<string, string>();
        }

        /// <summary>
        /// Starts the change monitor which begins polling for changes on the user's table specified in the constructor
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            await this.CreateWorkerTablesAsync();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            _ = Task.Run(() =>
            {
                this.CheckForChangesAsync(this._cancellationTokenSourceCheckForChanges.Token);
                this.RenewLeasesAsync(this._cancellationTokenSourceRenewLeases.Token);
            });
#pragma warning restore CS4014
        }

        /// <summary>
        /// Stops the change monitor which stops polling for changes on the user's table.
        /// If the change monitor is currently executing a set of changes, it is only stopped
        /// once execution is finished and the user's function is triggered (whether or not
        /// the trigger is successful) 
        /// </summary>
        /// <returns></returns>
        public void Stop()
        {
            this._cancellationTokenSourceCheckForChanges.Cancel();
        }

        /// <summary>
        /// Executed once every <see cref="LeaseTime"/> period. 
        /// If the state of the change monitor is <see cref="State.ProcessingChanges"/>, then 
        /// we will renew the leases held by the change monitor on "_rows"
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
                    await this._rowsLock.WaitAsync(token);
                    try
                    {
                        if (this._state == State.ProcessingChanges)
                        {
                            await this.RenewLeasesAsync();
                        }
                    }
                    catch (Exception e)
                    {
                        // This catch block is necessary so that the finally block is executed even in the case of an exception
                        // (see https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/try-finally, third paragraph)
                        // If we fail to renew the leases, multiple workers could be processing the same change data, but we have functionality
                        // in place to deal with this (see design doc)
                        this._logger.LogError($"Failed to renew leases due to error: {e.Message}");
                    }
                    finally
                    {
                        if (this._state == State.ProcessingChanges)
                        {
                            // Do we want to update this count even in the case of a failure to renew the leases? Probably, because
                            // the count is simply meant to indicate how much time the other thread has spent processing changes essentially
                            this._leaseRenewalCount++;
                            // If this thread has been cancelled, then the _cancellationTokenSourceExecutor could have already been disposed so
                            // shouldn't cancel it
                            if (this._leaseRenewalCount == MaxLeaseRenewalCount && !token.IsCancellationRequested)
                            {
                                // If we keep renewing the leases, the thread responsible for processing the changes is stuck
                                // If it's stuck, it has to be stuck in the function execution call (I think), so we should cancel the call
                                this._logger.LogWarning($"Call to execute the function (TryExecuteAsync) seems to be stuck, so it is being cancelled");
                                this._cancellationTokenSourceExecutor.Cancel();
                                this._cancellationTokenSourceExecutor.Dispose();
                                this._cancellationTokenSourceExecutor = new CancellationTokenSource();
                            }
                        }
                        // Want to always release the lock at the end, even if renewing the leases failed
                        this._rowsLock.Release();
                    }
                    // Want to make sure to renew the leases before they expire, so we renew them twice per lease period
                    await Task.Delay(TimeSpan.FromSeconds(LeaseIntervalInSeconds / 2), token);
                }
            }
            catch (Exception e)
            {
                // Only want to log the exception if it wasn't caused by StopAsync being called, since Task.Delay throws an exception
                // if it's cancelled
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

        /// <summary>
        /// Executed once every <see cref="PollingIntervalInSeconds"/> period. If the state of the change monitor is <see cref="State.CheckingForChanges"/>, then 
        /// the method query the change/worker tables for changes on the user's table. If any are found, the state of the change monitor is
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
                    if (this._state == State.CheckingForChanges)
                    {
                        // What should we do if this call gets stuck?
                        await this.CheckForChangesAsync();

                        if (this._rows.Count > 0)
                        {
                            this._state = State.ProcessingChanges;
                            IEnumerable<SqlChangeTrackingEntry<T>> entries = null;

                            try
                            {
                                // What should we do if this fails? It doesn't make sense to retry since it's not a connection based thing
                                // We could still try to trigger on the correctly processed entries, but that adds additional complication because
                                // we don't want to release the leases on the incorrectly processed entries
                                // For now, just give up I guess?
                                entries = this.GetSqlChangeTrackingEntries();
                            }
                            catch (Exception e)
                            {
                                await this.ClearRows($"Failed to extract user table data from table {this._userTable} associated with change metadata due to error: {e.Message}", true);
                            }

                            if (entries != null)
                            {
                                FunctionResult result = await this._executor.TryExecuteAsync(new TriggeredFunctionData() { TriggerValue = entries },
                                    this._cancellationTokenSourceExecutor.Token);
                                if (result.Succeeded)
                                {
                                    await this.ReleaseLeasesAsync();
                                }
                                else
                                {
                                    // In the future might make sense to retry executing the function, but for now we just let another worker try
                                    await this.ClearRows($"Failed to trigger user's function for table {this._userTable} due to error: {result.Exception.Message}", true);
                                }
                            }
                        }
                    }
                    // The Delay will exit if the token is cancelled
                    await Task.Delay(TimeSpan.FromSeconds(PollingIntervalInSeconds), token);
                }
            }
            catch (Exception e)
            {
                // Only want to log the exception if it wasn't caused by StopAsync being called, since Task.Delay throws an exception
                // if it's cancelled
                if (e.GetType() != typeof(TaskCanceledException))
                {
                    this._logger.LogError(e.Message);
                }
            }
            finally
            {
                // If this thread exits due to any reason, then the lease renewal thread should exit as well. Otherwise, it will keep looping
                // perpetually. 
                this._cancellationTokenSourceRenewLeases.Cancel();
                this._cancellationTokenSourceCheckForChanges.Dispose();
                this._cancellationTokenSourceExecutor.Dispose();
            }
        }

        /// <summary>
        /// Creates the worker table associated with the user's table, if one does not already exist.
        /// Also creates the global state and worker batch sizes tables for this DB if they do not already exist.
        /// Inserts a row into the global state table for this user table if one does not already exist, and inserts
        /// a row for this worker ID and user table into the worker batch sizes table if one does not already exist
        /// </summary>
        private async Task CreateWorkerTablesAsync()
        {
            await this.GetUserTableSchemaAsync();
            this._workerTable = $"[{Schema}].[Worker_Table_{this._userTableId}_{this._workerId}]";

            using var connection = new SqlConnection(this._connectionString);
            await connection.OpenAsync();
            using SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead);
            // Create the schema where the worker tables will be located if it does not already exist
            using (SqlCommand createSchemaCommand = BuildCreateSchemaCommand(connection, transaction))
            {
                await createSchemaCommand.ExecuteNonQueryAsync();
            }

            // Create the global state table, if one doesn't already exist for this database
            using (SqlCommand createGlobalStateTableCommand = this.BuildCreateGlobalStateTableCommand(connection, transaction))
            {
                await createGlobalStateTableCommand.ExecuteNonQueryAsync();
            }

            // Insert a row into the global state table for this user table, if one doesn't already exist
            using (SqlCommand insertRowGlobalStateTableCommand = this.BuildInsertRowGlobalStateTableCommand(connection, transaction))
            {
                try
                {
                    await insertRowGlobalStateTableCommand.ExecuteNonQueryAsync();
                }
                // Could fail if we try to insert a NULL value into the GlobalVersionNumber, which happens when CHANGE_TRACKING_MIN_VALID_VERSION 
                // returns NULL for the user table, meaning that change tracking is not enabled for either the database or table (or both)
                catch (Exception e)
                {
                    string errorMessage = $"Failed to start processing changes to table {this._userTable}, potentially because change tracking was not " +
                        $"enabled for the table or database {connection.Database}.";
                    this._logger.LogWarning(errorMessage + $" Exact exception thrown is {e.Message}");
                    throw new InvalidOperationException(errorMessage);
                }
            }

            // Create the worker table, if one doesn't already exist for this user table
            using (SqlCommand createWorkerTableCommand = this.BuildCreateWorkerTableCommand(connection, transaction))
            {
                await createWorkerTableCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        /// <summary>
        /// Retrieves the primary keys of the user's table and stores them in the _primaryKeys dictionary,
        /// which maps from primary key name to primary key type
        /// Also retrieves the column names of the user's table and stores them in _userTableColumns,
        /// as well as the user table's OBJECT_ID which it stores to _userTableId
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the query to retrieve the OBJECT_ID of the user table fails to correctly execute
        /// This can happen if the OBJECT_ID call returns NULL, meaning that the user table might not exist in the database
        /// </exception>
        private async Task GetUserTableSchemaAsync()
        {
            using (var connection = new SqlConnection(this._connectionString))
            {
                // I don't think I need a transaction for this since the command just reads data
                await connection.OpenAsync();
                // Determine the primary keys of the user table
                using (SqlCommand getPrimaryKeysCommand = this.BuildGetUserTablePrimaryKeysCommand(connection))
                {
                    using SqlDataReader reader = await getPrimaryKeysCommand.ExecuteReaderAsync();
                    await this.DeterminePrimaryKeyTypesAsync(reader);
                }

                this._userTableColumns.Clear();
                // Determine the names of the user table columns
                using (SqlCommand getColumnNamesCommand = this.BuildGetUserTableColumnNamesCommand(connection))
                {
                    using SqlDataReader reader = await getColumnNamesCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        this._userTableColumns.Add(reader.GetString(0));
                    }
                }

                this.InitializeQueryStrings();
            }
            this._userTableId = await GetUserTableIDAsync(this._connectionString, this._userTable);
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
                using var connection = new SqlConnection(this._connectionString);
                await connection.OpenAsync();
                using SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead);
                // Update the version number stored in the global state table if necessary before using it 
                using (SqlCommand updateGlobalVersionNumberCommand = this.BuildUpdateGlobalVersionNumberCommand(connection, transaction))
                {
                    await updateGlobalVersionNumberCommand.ExecuteNonQueryAsync();
                }

                // Use the version number to query for new changes
                using (SqlCommand getChangesCommand = this.BuildCheckForChangesCommand(connection, transaction))
                {
                    using SqlDataReader reader = await getChangesCommand.ExecuteReaderAsync();
                    var cols = new List<string>();
                    while (await reader.ReadAsync())
                    {
                        this._rows.Add(SqlBindingUtilities.BuildDictionaryFromSqlRow(reader, cols));
                    }
                }

                // If changes were found, acquire leases on them
                if (this._rows.Count != 0)
                {
                    using SqlCommand acquireLeaseCommand = this.BuildAcquireLeasesCommand(connection, transaction);
                    await acquireLeaseCommand.ExecuteNonQueryAsync();
                }
                await transaction.CommitAsync();
            }
            catch (Exception e)
            {
                // If there's an exception in any part of the process, we want to clear all of our data in memory and retry
                // checking for changes again
                this._rows.Clear();
                this._whereChecks.Clear();
                this._logger.LogWarning($"Failed to check {this._userTable} for new changes due to error: {e.Message}");
            }
        }

        /// <summary>
        /// Renews the leases held on _rows
        /// </summary>
        private async Task RenewLeasesAsync()
        {
            using var connection = new SqlConnection(this._connectionString);
            await connection.OpenAsync();
            // I don't think I need a transaction for renewing leases. If this worker reads in a row from the worker table
            // and determines that it corresponds to its batch of changes, but then that row gets deleted by a cleanup task,
            // it shouldn't renew its lease on it anyways
            using SqlCommand renewLeaseCommand = this.BuildRenewLeasesCommand(connection);
            await renewLeaseCommand.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Resets the in-memory state of the change monitor and sets it to start polling for changes again.
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
                this._logger.LogError(error);
                await this._rowsLock.WaitAsync();
            }
            this._leaseRenewalCount = 0;
            this._rows.Clear();
            this._whereChecks.Clear();
            this._state = State.CheckingForChanges;
            this._rowsLock.Release();
        }

        /// <summary>
        /// Releases the leases held on _rows
        /// </summary>
        /// <returns></returns>
        private async Task ReleaseLeasesAsync()
        {
            // Don't want to change the _rows while another thread is attempting to renew leases on them
            await this._rowsLock.WaitAsync();
            long newVersionNumber = this.CalculateNewVersionNumber();
            try
            {
                using var connection = new SqlConnection(this._connectionString);
                await connection.OpenAsync();
                using SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead);
                // Release the leases held on _rows
                using (SqlCommand releaseLeaseCommand = this.BuildReleaseLeasesCommand(connection, transaction))
                {
                    await releaseLeaseCommand.ExecuteNonQueryAsync();
                }

                // Update the global state table if we have processed all changes with version number <= newVersionNumber, and clean up the worker table
                // to remove all rows with VersionNumber <= newVersionNumber
                using (SqlCommand updateGlobalStateTableCommand = this.BuildUpdateGlobalStateTableCommand(connection, transaction, newVersionNumber))
                {
                    await updateGlobalStateTableCommand.ExecuteNonQueryAsync();
                }
                await transaction.CommitAsync();

            }
            catch (Exception e)
            {
                // What should we do if releasing the leases fails? We could try to release them again or just wait,
                // since eventually the lease time will expire. Then another thread will re-process the same changes though,
                // so less than ideal. But for now that's the functionality
                this._logger.LogError($"Failed to release leases for user table {this._userTable} due to error: {e.Message}");
            }
            finally
            {
                // Want to do this before releasing the lock in case the renew leases thread wakes up. It will see that
                // the state is checking for changes and not renew the (just released) leases
                await this.ClearRows(string.Empty, false);
            }
        }

        /// <summary>
        /// Builds the command to determine the primary key column names and types of the user table
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildGetUserTablePrimaryKeysCommand(SqlConnection connection)
        {
            string getUserTablePrimaryKeysQuery =
                $"SELECT c.name, t.name, c.max_length, c.precision, c.scale\n" +
                $"FROM sys.indexes i\n" +
                $"INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id\n" +
                $"INNER JOIN sys.columns c ON ic.object_id = c.object_id AND c.column_id = ic.column_id\n" +
                $"INNER JOIN sys.types t ON c.user_type_id = t.user_type_id\n" +
                $"WHERE i.is_primary_key = 1 and i.object_id = OBJECT_ID(N\'{this._userTable}\', \'U\');";

            return new SqlCommand(getUserTablePrimaryKeysQuery, connection);
        }

        /// <summary>
        /// Builds the command to determine the names of the user table columns
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildGetUserTableColumnNamesCommand(SqlConnection connection)
        {
            string getUserTableColumnNamesQuery =
                $"SELECT name\n" +
                $"FROM sys.columns\n" +
                $"WHERE object_id = OBJECT_ID(\'{this._userTable}\');";

            return new SqlCommand(getUserTableColumnNamesQuery, connection);
        }

        /// <summary>
        /// Builds the command to update the global state table in the case of a new minimum valid version number
        /// Sets the GlobalVersionNumber for this _userTable to be the new minimum valid version number
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildUpdateGlobalVersionNumberCommand(SqlConnection connection, SqlTransaction transaction)
        {
            string updateGlobalStateTableCommand = $@"
                DECLARE @min_version bigint;
                DECLARE @current_version bigint;
                SET @min_version = CHANGE_TRACKING_MIN_VALID_VERSION({this._userTableId});
                SELECT @current_version = GlobalVersionNumber FROM {this._globalStateTable} WHERE UserTableID = {this._userTableId} AND WorkerID = '{this._workerId}';
                
                IF @current_version < @min_version
                    UPDATE {this._globalStateTable} SET GlobalVersionNumber = @min_version WHERE UserTableID = {this._userTableId} AND WorkerID = '{this._workerId}';
            ";

            return new SqlCommand(updateGlobalStateTableCommand, connection, transaction);
        }

        /// <summary>
        /// Builds the query to check for changes on the user's table (<see cref="CheckForChangesAsync()"/>)
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildCheckForChangesCommand(SqlConnection connection, SqlTransaction transaction)
        {
            string getChangesQuery = $@"
                DECLARE @version bigint;
                SELECT @version = GlobalVersionNumber FROM {this._globalStateTable} WHERE UserTableID = {this._userTableId} AND WorkerID = '{this._workerId}';
                SELECT TOP {BatchSize} * FROM
                    (SELECT {this._primaryKeysSelectList}, {this._userTableColumnsSelectList}
                        c.SYS_CHANGE_VERSION, c.SYS_CHANGE_OPERATION,
                        w.VersionNumber, w.DequeueCount, w.LeaseExpirationTime
                    FROM CHANGETABLE (CHANGES {this._userTable}, @version) AS c
                    LEFT OUTER JOIN {this._workerTable} AS w with (TABLOCKX) ON {this._leftOuterJoinWorkerTable}
                    LEFT OUTER JOIN {this._userTable} AS u ON {this._leftOuterJoinUserTable}) AS Changes
                WHERE
                    (Changes.LeaseExpirationTime IS NULL
                        AND (Changes.VersionNumber IS NULL OR Changes.VersionNumber < Changes.SYS_CHANGE_VERSION)
                        OR Changes.LeaseExpirationTime < SYSDATETIME())
                    AND (Changes.DequeueCount IS NULL OR Changes.DequeueCount < {MaxDequeueCount})
                ORDER BY Changes.SYS_CHANGE_VERSION ASC;
            ";

            return new SqlCommand(getChangesQuery, connection, transaction);
        }

        /// <summary>
        /// Builds the query to acquire leases on the rows in "_rows" if changes are detected in the user's table (<see cref="CheckForChangesAsync()"/>)
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildAcquireLeasesCommand(SqlConnection connection, SqlTransaction transaction)
        {
            var acquireLeasesCommand = new SqlCommand();
            SqlBindingUtilities.AddPrimaryKeyParametersToCommand(acquireLeasesCommand, this._rows, this._primaryKeys.Keys);
            string acquireLeasesCommandString = string.Empty;
            int index = 0;

            foreach (Dictionary<string, string> row in this._rows)
            {
                string whereCheck = string.Join(" AND ", this._primaryKeys.Keys.Select(key => $"{key} = @{key}_{index}"));
                string valuesList = string.Join(", ", this._primaryKeys.Keys.Select(key => $"@{key}_{index}"));
                this._whereChecks.Add(whereCheck);

                row.TryGetValue("SYS_CHANGE_VERSION", out string versionNumber);
                acquireLeasesCommandString +=
                    $"IF NOT EXISTS (SELECT * FROM {this._workerTable} with (TABLOCKX) WHERE {whereCheck})\n" +
                    $"INSERT INTO {this._workerTable} with (TABLOCKX)\n" +
                    $"VALUES ({valuesList}, DATEADD(s, {LeaseIntervalInSeconds}, SYSDATETIME()), 0, {versionNumber})\n" +
                    $"ELSE\n" +
                    $"UPDATE {this._workerTable} with (TABLOCKX)\n" +
                    $"SET LeaseExpirationTime = DATEADD(s, {LeaseIntervalInSeconds}, SYSDATETIME()), DequeueCount = DequeueCount + 1, " +
                    $"VersionNumber = {versionNumber}\n" +
                    $"WHERE {whereCheck};\n";

                index++;
            }

            acquireLeasesCommand.CommandText = acquireLeasesCommandString;
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
            var renewLeasesCommand = new SqlCommand();
            SqlBindingUtilities.AddPrimaryKeyParametersToCommand(renewLeasesCommand, this._rows, this._primaryKeys.Keys);
            string renewLeasesCommandString = string.Empty;
            int index = 0;

            foreach (Dictionary<string, string> row in this._rows)
            {
                renewLeasesCommandString +=
                $"UPDATE {this._workerTable} with (TABLOCKX)\n" +
                $"SET LeaseExpirationTime = DATEADD(s, {LeaseIntervalInSeconds}, SYSDATETIME())\n" +
                $"WHERE {this._whereChecks.ElementAt(index++)};\n";
            }

            renewLeasesCommand.CommandText = renewLeasesCommandString;
            renewLeasesCommand.Connection = connection;

            return renewLeasesCommand;
        }

        /// <summary>
        /// Builds the query to release leases on the rows in "_rows" after successful invocation of the user's function (<see cref="CheckForChangesAsync()"/>)
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildReleaseLeasesCommand(SqlConnection connection, SqlTransaction transaction)
        {
            var releaseLeasesCommand = new SqlCommand();
            string releaseLeasesCommandString = $"DECLARE @current_version bigint;\n";
            SqlBindingUtilities.AddPrimaryKeyParametersToCommand(releaseLeasesCommand, this._rows, this._primaryKeys.Keys);
            int index = 0;

            foreach (Dictionary<string, string> row in this._rows)
            {
                string whereCheck = this._whereChecks.ElementAt(index++);
                row.TryGetValue("SYS_CHANGE_VERSION", out string versionNumber);

                releaseLeasesCommandString +=
                    $"SELECT @current_version = VersionNumber\n" +
                    $"FROM {this._workerTable} with (TABLOCKX) \n" +
                    $"WHERE {whereCheck};\n" +
                    $"IF {versionNumber} >= @current_version\n" +
                    $"UPDATE {this._workerTable} with (TABLOCKX) \n" +
                    $"SET LeaseExpirationTime = NULL, DequeueCount = 0, VersionNumber = {versionNumber}\n" +
                    $"WHERE {whereCheck};\n";
            }

            releaseLeasesCommand.CommandText = releaseLeasesCommandString;
            releaseLeasesCommand.Connection = connection;
            releaseLeasesCommand.Transaction = transaction;

            return releaseLeasesCommand;
        }

        /// <summary>
        /// Builds the command to create the worker table if one does not already exist (<see cref="CreateWorkerTablesAsync"/>)
        /// </summary>
        /// <param name="connection">The connection to attach to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildCreateWorkerTableCommand(SqlConnection connection, SqlTransaction transaction)
        {

            string primaryKeysWithTypes = string.Join(",\n", this._primaryKeys.Select(pair => $"{pair.Key} {pair.Value}"));
            string primaryKeysList = string.Join(", ", this._primaryKeys.Keys);

            string createWorkerTableCommand =
                $"IF OBJECT_ID(N\'{this._workerTable}\', \'U\') IS NULL\n" +
                $"CREATE TABLE {this._workerTable} (\n" +
                $"{primaryKeysWithTypes},\n" +
                $"LeaseExpirationTime datetime2,\n" +
                $"DequeueCount int,\n" +
                $"VersionNumber bigint\n" +
                $"PRIMARY KEY({primaryKeysList})\n" +
                $");\n";

            return new SqlCommand(createWorkerTableCommand, connection, transaction);
        }

        /// <summary>
        /// Builds the command to create the schema where the worker tables are located if it does not already exist (<see cref="CreateWorkerTablesAsync"/>)
        /// </summary>
        /// <param name="connection">The connection to attach to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private static SqlCommand BuildCreateSchemaCommand(SqlConnection connection, SqlTransaction transaction)
        {
            string createSchemaCommand =
                $"IF SCHEMA_ID(N\'{Schema}\') IS NULL\n" +
                $"EXEC (\'CREATE SCHEMA {Schema}\')";

            return new SqlCommand(createSchemaCommand, connection, transaction);
        }

        /// <summary>
        /// Builds the command to create the global state table if one does not already exist (<see cref="CreateWorkerTablesAsync"/>)
        /// </summary>
        /// <param name="connection">The connection to attach to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildCreateGlobalStateTableCommand(SqlConnection connection, SqlTransaction transaction)
        {
            string createGlobalStateTableCommand = $@"
                IF OBJECT_ID(N'{this._globalStateTable}', N'U') IS NULL
                    CREATE TABLE {this._globalStateTable} (
                        UserTableID int,
                        WorkerID char(80),
                        GlobalVersionNumber bigint NOT NULL,
                        PRIMARY KEY (UserTableID, WorkerID)
                    );
            ";

            return new SqlCommand(createGlobalStateTableCommand, connection, transaction);
        }

        /// <summary>
        /// Builds the command to insert a row into the global state table for this user table, if such a row doesn't already exist
        /// </summary>
        /// <param name="connection">The connection to attach to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildInsertRowGlobalStateTableCommand(SqlConnection connection, SqlTransaction transaction)
        {
            string insertRowGlobalStateTableCommand = $@"
                IF NOT EXISTS (SELECT * FROM {this._globalStateTable} WHERE UserTableID = {this._userTableId} AND WorkerID = '{this._workerId}')
                    INSERT INTO {this._globalStateTable}
                    VALUES ({this._userTableId}, '{this._workerId}', CHANGE_TRACKING_MIN_VALID_VERSION({this._userTableId}));
            ";

            return new SqlCommand(insertRowGlobalStateTableCommand, connection, transaction);
        }

        /// <summary>
        /// Builds the command to update the global version number in _globalStateTable after successful invocation of the user's function
        /// If the global version number is updated, also cleans the worker table and removes all rows for which VersionNumber <= newVersionNumber
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <param name="newVersionNumber">The new GlobalVersionNumber to store in the _globalStateTable for this _userTable</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildUpdateGlobalStateTableCommand(SqlConnection connection, SqlTransaction transaction, long newVersionNumber)
        {
            if (connection is null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (transaction is null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            string updateGlobalStateTableCommand = $@"
                DECLARE @current_version bigint;
                DECLARE @unprocessed_changes bigint;
                SELECT @current_version = GlobalVersionNumber FROM {this._globalStateTable} WHERE UserTableID = {this._userTableId} AND WorkerID = '{this._workerId}';

                SELECT @unprocessed_changes = COUNT(*) FROM
                    (SELECT c.SYS_CHANGE_VERSION FROM CHANGETABLE(CHANGES {this._userTable}, @current_version) AS c
                    LEFT OUTER JOIN {this._workerTable} AS w with (TABLOCKX) ON {this._leftOuterJoinWorkerTable}
                    WHERE c.SYS_CHANGE_VERSION <= {newVersionNumber}
                    AND ((w.VersionNumber IS NULL OR w.VersionNumber != c.SYS_CHANGE_VERSION OR w.LeaseExpirationTime IS NOT NULL)
                    AND (w.DequeueCount IS NULL OR w.DequeueCount < {MaxDequeueCount}))) AS Changes;

                IF @unprocessed_changes = 0 AND {newVersionNumber} > @current_version
                BEGIN
                    UPDATE {this._globalStateTable} SET GlobalVersionNumber = {newVersionNumber} WHERE UserTableID = {this._userTableId} AND WorkerID = '{this._workerId}';
                    DELETE FROM {this._workerTable} with (TABLOCKX) WHERE VersionNumber <= {newVersionNumber};
                END
            ";

            return new SqlCommand(updateGlobalStateTableCommand, connection, transaction);
        }

        /// <summary>
        /// Adds the primary key name (first column returned by the reader) and type to the _primaryKeys dictionary.
        /// Adds length arguments if the type of any of those listed in variableLengthTypes, and precision and 
        /// scale arguments if it is any of those listed in variablePrecisionTypes
        /// Otherwise, if the type accepts arguments (like datetime2), just uses the default which is the highest
        /// precision for all other types
        /// </summary>
        /// <param name="reader">Contains each primary key name and corresponding type information</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if no primary keys are found for the user table. This could be because the user table does not have
        /// any primary key columns.
        /// </exception>
        private async Task DeterminePrimaryKeyTypesAsync(SqlDataReader reader)
        {
            // Necessary in the case that a prior attempt to start the SqlTableChangeMonitor failed.
            // Could be the case that these were partially populated, so should clear and repopulate them
            this._primaryKeys.Clear();

            while (await reader.ReadAsync())
            {
                string type = reader.GetString(1);
                if (this.variableLengthTypes.Contains(type))
                {
                    short length = reader.GetInt16(2);
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
                else if (this.variablePrecisionTypes.Contains(type))
                {
                    int precision = reader.GetByte(3);
                    int scale = reader.GetByte(4);
                    type += "(" + precision + "," + scale + ")";
                }
                this._primaryKeys.Add(reader.GetString(0), type);
            }

            if (this._primaryKeys.Count == 0)
            {
                throw new InvalidOperationException($"Unable to determine the primary keys of user table {this._userTable}. Potentially, the table does not have any primary " +
                    $"key columns. A primary key is required for every user table for which changes are being monitored.");
            }
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
            foreach (Dictionary<string, string> row in this._rows)
            {
                SqlChangeType changeType = GetChangeType(row);
                // If the row has been deleted, there is no longer any data for it in the user table. The best we can do
                // is populate the entry with the primary key values of the row
                if (changeType == SqlChangeType.Deleted)
                {
                    entries.Add(new SqlChangeTrackingEntry<T>(changeType, JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(this.BuildDefaultDictionary(row)))));
                }
                else
                {
                    var userTableRow = new Dictionary<string, string>();
                    foreach (string col in this._userTableColumns)
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
            if (changeType.Equals("I", StringComparison.Ordinal))
            {
                return SqlChangeType.Inserted;
            }
            else if (changeType.Equals("U", StringComparison.Ordinal))
            {
                return SqlChangeType.Updated;
            }
            else if (changeType.Equals("D", StringComparison.Ordinal))
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
            foreach (string primaryKey in this._primaryKeys.Keys)
            {
                row.TryGetValue(primaryKey, out string primaryKeyValue);
                defaultDictionary.Add(primaryKey, primaryKeyValue);
            }
            return defaultDictionary;
        }

        /// <summary>
        /// Populates the query strings. Ensure that they will be initialized after _primaryKeys and _userTableColumns are populated.
        /// </summary>
        private void InitializeQueryStrings()
        {
            this._primaryKeysSelectList = string.Join(", ", this._primaryKeys.Keys.Select(key => $"c.{key}"));
            this._leftOuterJoinWorkerTable = string.Join(" AND ", this._primaryKeys.Keys.Select(key => $"c.{key} = w.{key}"));
            this._leftOuterJoinUserTable = string.Join(" AND ", this._primaryKeys.Keys.Select(key => $"c.{key} = u.{key}"));

            var nonPrimaryKeyCols = this._userTableColumns.Where(col => !this._primaryKeys.ContainsKey(col)).ToList();
            this._userTableColumnsSelectList =
                string.Join(", ", nonPrimaryKeyCols.Select(col => $"u.{col}")) + (nonPrimaryKeyCols.Any() ? ", " : string.Empty);
        }

        /// <summary>
        /// Calculates the new version number to attempt to update GlobalVersionNumber in global state table to
        /// If all version numbers in _rows are the same, use that version number
        /// If they aren't, use the second largest version number
        /// For an explanation as to why this method was chosen, see 9c in Steps of Operation in this design doc:
        /// https://microsoft-my.sharepoint.com/:w:/p/t-sotevo/EQdANWq9ZWpKm8e48TdzUwcBGZW07vJmLf8TL_rtEG8ixQ?e=owN2EX
        /// </summary>
        private long CalculateNewVersionNumber()
        {
            var versionNumbers = new SortedSet<long>();
            foreach (Dictionary<string, string> row in this._rows)
            {
                row.TryGetValue("SYS_CHANGE_VERSION", out string versionNumberString);
                versionNumbers.Add(long.Parse(versionNumberString, CultureInfo.InvariantCulture));
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

        private enum State
        {
            CheckingForChanges,
            ProcessingChanges
        }

        /// <summary>
        /// Returns the OBJECT_ID of userTable
        /// </summary>
        /// <param name="connectionString">The SQL connection string used to establish a connection to the user's database</param>
        /// <param name="userTable">The (sanitized) name of the user table</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the query to retrieve the OBJECT_ID of the user table fails to correctly execute
        /// This can happen if the OBJECT_ID call returns NULL, meaning that the user table might not exist in the database
        /// </exception>
        private static async Task<int> GetUserTableIDAsync(string connectionString, string userTable)
        {
            string getObjectIDQuery = $"SELECT OBJECT_ID(N\'{userTable}\', \'U\');";

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                // Don't think I need a transaction for this since I'm just reading data
                using var getObjectIDCommand = new SqlCommand(getObjectIDQuery, connection);
                using SqlDataReader reader = await getObjectIDCommand.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    object userTableID = reader.GetValue(0);
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
            throw new InvalidOperationException($"Failed to determine the OBJECT_ID of the user table {userTable}");
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}