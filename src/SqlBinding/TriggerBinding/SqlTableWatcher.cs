// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
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
    internal class SqlTableWatcher<T>
    {
        private readonly string _workerTable;
        private readonly string _userTable;
        private readonly string _connectionString;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly CancellationTokenSource _cancellationTokenSourceExecutor;
        private readonly CancellationTokenSource _cancellationTokenSourceCheckForChanges;
        private readonly CancellationTokenSource _cancellationTokenSourceRenewLeases;

        // It should be impossible for multiple threads to access these at the same time because of the semaphores we use
        private readonly List<Dictionary<string, string>> _rows;
        private readonly List<String> _userTableColumns;
        private readonly Dictionary<string, string> _primaryKeys;
        private readonly Dictionary<string, string> _queryStrings;
        private readonly SemaphoreSlim _leasesLock;
        private State _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTableWatcher{T}"> class
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
        public SqlTableWatcher(string table, string connectionString, ITriggeredFunctionExecutor executor)
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
            _userTable = SqlBindingUtilities.NormalizeTableName(table);
            _workerTable = SqlBindingUtilities.NormalizeTableName(BuildWorkerTableName(table));

            _cancellationTokenSourceExecutor = new CancellationTokenSource();
            _cancellationTokenSourceCheckForChanges = new CancellationTokenSource();
            _cancellationTokenSourceRenewLeases = new CancellationTokenSource();
            _leasesLock = new SemaphoreSlim(1);

            _rows = new List<Dictionary<string, string>>();
            _userTableColumns = new List<string>();
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
                    await _leasesLock.WaitAsync();
                    try
                    {
                        if (_state == State.ProcessingChanges)
                        {
                            await RenewLeasesAsync();
                        }
                    }
                    catch (Exception e)
                    {
                        // logger here
                    }
                    finally
                    {
                        // Want to always release the lock at the end, even if renewing the leases failed
                        _leasesLock.Release();
                    }
                    // Want to make sure to renew the leases before they expire, so we renew them twice per 
                    // lease period
                    await Task.Delay(SqlTriggerConstants.LeaseTime / 2 * 1000, token);
                }
            }
            catch (Exception e)
            {
                // have a logger here. could also be triggered by a TaskCancelledException
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
            try {
                while (!token.IsCancellationRequested)
                {
                    if (_state == State.CheckingForChanges)
                    {
                        await CheckForChangesAsync();
                        if (_rows.Count > 0)
                        {
                            IEnumerable<SqlChangeTrackingEntry<T>> entries = GetSqlChangeTrackingEntries();
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
                                // Should probably have some count for how many times we tried to execute the function. After a certain amount of times
                                // we should give up
                            }
                            if (token.IsCancellationRequested)
                            {
                                // Only want to cancel renewing leases after we finish processing the changes
                                _cancellationTokenSourceRenewLeases.Cancel();
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
                // have a logger here
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
                    foreach (var row in _rows)
                    {   
                        // Not great that we're doing a SqlCommand per row, should batch this
                        using (SqlCommand acquireLeaseCommand = BuildAcquireLeaseOnRowCommand(row, connection, transaction))
                        {
                            await acquireLeaseCommand.ExecuteNonQueryAsync();
                        }
                    }
                    await transaction.CommitAsync();
                }
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
                foreach (var row in _rows)
                {
                    // Not great that we're doing a SqlCommand per row, should batch this
                    using (SqlCommand renewLeaseCommand = BuildRenewLeaseOnRowCommand(row, connection))
                    {
                        await renewLeaseCommand.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Releases the leases held on _rows
        /// </summary>
        /// <returns></returns>
        private async Task ReleaseLeasesAsync()
        {
            // Don't want to change the _rows while another thread is attempting to renew leases on them
            await _leasesLock.WaitAsync();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
                    {
                        foreach (var row in _rows)
                        {
                            // Not great that we're doing a SqlCommand per row, should batch this
                            using (SqlCommand releaseLeaseCommand = BuildReleaseLeaseOnRowCommand(row, connection, transaction))
                            {
                                await releaseLeaseCommand.ExecuteNonQueryAsync();
                            }
                        }
                        await transaction.CommitAsync();
                    }
                }
                _rows.Clear();
            }
            catch (Exception e)
            {
                // What should we do if releasing the leases fails? We could try to release them again or just wait,
                // since eventually the lease time will expire. Then another thread will re-process the same changes though,
                // so less than ideal
            }
            finally
            {
                // Want to do this before releasing the lock in case the renew leases thread wakes up. It will see that
                // the state is checking for changes and not renew the (just released) leases
                // Only want to change the state if it was previously ProcessingChanges though. If it is stopped, for example,
                // we don't want to start polling for changes again
                _state = State.CheckingForChanges;
                _leasesLock.Release();
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
        /// <param name="row">The row that the lease will be acquired on</param>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildAcquireLeaseOnRowCommand(Dictionary<string, string> row, SqlConnection connection, SqlTransaction transaction)
        {
            var acquireLeaseCommand = new SqlCommand();
            SqlBindingUtilities.AddPrimaryKeyParametersToCommand(acquireLeaseCommand, row, _primaryKeys.Keys);

            string valuesList;
            // If one isn't in the map, neither is
            if (!_queryStrings.TryGetValue(SqlTriggerConstants.WhereCheck, out string whereCheck))
            {
                whereCheck = string.Join(" AND ", _primaryKeys.Keys.Select(key => $"{key} = @{key}"));
                valuesList = string.Join(", ", _primaryKeys.Keys.Select(key => $"@{key}"));
                _queryStrings.Add(SqlTriggerConstants.WhereCheck, whereCheck);
                _queryStrings.Add(SqlTriggerConstants.PrimaryKeyValues, valuesList);
            }
            else
            {
                _queryStrings.TryGetValue(SqlTriggerConstants.PrimaryKeyValues, out valuesList);
            }

            row.TryGetValue("SYS_CHANGE_VERSION", out string versionNumber);

            var acquireLeaseOnRow =
                $"IF NOT EXISTS (SELECT * FROM {_workerTable} WHERE {whereCheck})\n" +
                $"INSERT INTO {_workerTable}\n" +
                $"VALUES ({valuesList}, DATEADD({SqlTriggerConstants.LeaseUnits}, {SqlTriggerConstants.LeaseTime}, SYSDATETIME()), 0, {versionNumber})\n" +
                $"ELSE\n" +
                $"UPDATE {_workerTable}\n" +
                $"SET LeaseExpirationTime = DATEADD({SqlTriggerConstants.LeaseUnits}, {SqlTriggerConstants.LeaseTime}, SYSDATETIME()), DequeueCount = DequeueCount + 1, " +
                $"VersionNumber = {versionNumber}\n" +
                $"WHERE {whereCheck};";

            acquireLeaseCommand.CommandText = acquireLeaseOnRow;
            acquireLeaseCommand.Connection = connection;
            acquireLeaseCommand.Transaction = transaction;
            return acquireLeaseCommand;
        }

        /// <summary>
        /// Builds the query to renew leases on the rows in "_rows" (<see cref="RenewLeasesAsync(CancellationToken)"/>)
        /// </summary>
        /// <param name="row">The row that the lease will be renewed on</param>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildRenewLeaseOnRowCommand(Dictionary<string, string> row, SqlConnection connection)
        {
            SqlCommand renewLeaseCommand = new SqlCommand();

            _queryStrings.TryGetValue(SqlTriggerConstants.WhereCheck, out string whereCheck);
            SqlBindingUtilities.AddPrimaryKeyParametersToCommand(renewLeaseCommand, row, _primaryKeys.Keys);

            var renewLeaseOnRow =
                $"UPDATE {_workerTable}\n" +
                $"SET LeaseExpirationTime = DATEADD({SqlTriggerConstants.LeaseUnits}, {SqlTriggerConstants.LeaseTime}, SYSDATETIME())\n" +
                $"WHERE {whereCheck};";

            renewLeaseCommand.CommandText = renewLeaseOnRow;
            renewLeaseCommand.Connection = connection;

            return renewLeaseCommand;
        }

        /// <summary>
        /// Builds the query to release leases on the rows in "_rows" after successful invocation of the user's function (<see cref="CheckForChangesAsync"/>)
        /// </summary>
        /// <param name="row">The row that the lease will be released on</param>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildReleaseLeaseOnRowCommand(Dictionary<string, string> row, SqlConnection connection, SqlTransaction transaction)
        {
            SqlCommand releaseLeaseCommand = new SqlCommand();
            _queryStrings.TryGetValue(SqlTriggerConstants.WhereCheck, out string whereCheck);
            row.TryGetValue("SYS_CHANGE_VERSION", out string versionNumber);
            SqlBindingUtilities.AddPrimaryKeyParametersToCommand(releaseLeaseCommand, row, _primaryKeys.Keys);

            var releaseLeaseOnRow =
                $"DECLARE @current_version bigint;\n" +
                $"SELECT @current_version = VersionNumber\n" +
                $"FROM {_workerTable}\n" + 
                $"WHERE {whereCheck};\n" +
                $"IF {versionNumber} >= @current_version\n" +
                $"UPDATE {_workerTable}\n" +
                $"SET LeaseExpirationTime = NULL, DequeueCount = 0, VersionNumber = {versionNumber}\n" +
                $"WHERE {whereCheck};";

            releaseLeaseCommand.CommandText = releaseLeaseOnRow;
            releaseLeaseCommand.Connection = connection;
            releaseLeaseCommand.Transaction = transaction;

            return releaseLeaseCommand;
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
        /// If any of the entries correspond to a deleted row, then the <see cref="SqlChangeTrackingEntry{T}.Data"/> is populated
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