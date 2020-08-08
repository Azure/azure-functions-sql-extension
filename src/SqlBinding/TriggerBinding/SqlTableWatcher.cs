// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
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
    internal class SqlTableWatcher
    {
        private readonly string _workerTable;
        private readonly string _userTable;
        private readonly string _connectionStringSetting;
        private readonly IConfiguration _configuration;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly CancellationTokenSource _cancellationTokenSource;

        // It should be impossible for multiple threads to access these at the same time because of the semaphores we use
        private readonly Dictionary<string, string> _primaryKeys;
        private readonly List<Dictionary<string, string>> _rows;
        private readonly Dictionary<string, string> _queryStrings;
        private readonly Timer _renewLeasesTimer;
        private readonly Timer _checkForChangesTimer;
        private readonly SemaphoreSlim _checkForChangesLock;
        private readonly SemaphoreSlim _renewLeasesLock;
        private readonly EventWaitHandle _signalStopHandle;
        private readonly EventWaitHandle _signalStoppedHandle;

        // Can't use an enum for these because it doesn't work with the Interlocked class
        private int _state;
        private const int CheckingForChanges = 0;
        private const int ProcessingChanges = 1;
        private const int Stopped = 2;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTableWatcher"/> class.
        /// </summary>
        /// <param name="connectionStringSetting"> 
        /// The name of the app setting that stores the SQL connection string
        /// </param>
        /// <param name="table"> 
        /// The name of the user table that changes are being tracked on
        /// </param>
        /// <param name="configuration">
        /// Used to extract the connection string from connectionStringSetting
        /// </param>
        /// <param name="executor">
        /// Used to execute the user's function when changes are detected on "table"
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any configuration, connectionStringSetting, or executor are null
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if table is null or empty
        /// </exception>
        public SqlTableWatcher(string table, string connectionStringSetting, IConfiguration configuration, ITriggeredFunctionExecutor executor)
        {
            if (string.IsNullOrEmpty(table))
            {
                throw new ArgumentException("User table name cannot be null or empty");
            }
            _connectionStringSetting = connectionStringSetting ?? throw new ArgumentNullException(nameof(connectionStringSetting));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _userTable = SqlBindingUtilities.ProcessTableName(table);
            _workerTable = SqlBindingUtilities.ProcessTableName(BuildWorkerTableName(table));

            _cancellationTokenSource = new CancellationTokenSource();
            _rows = new List<Dictionary<string, string>>();
            _queryStrings = new Dictionary<string, string>();
            _primaryKeys = new Dictionary<string, string>();

            _checkForChangesTimer = new Timer(CheckForChangesCallback);
            _renewLeasesTimer = new Timer(RenewLeasesCallback);
            // Should these be just normal semaphores?
            _checkForChangesLock = new SemaphoreSlim(1);
            _renewLeasesLock = new SemaphoreSlim(1);
            _signalStoppedHandle = new AutoResetEvent(false);

        }

        /// <summary>
        /// Starts the watcher which begins polling for changes on the user's table specified in the constructor
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            await CreateWorkerTableAsync();
            _checkForChangesTimer.Change(0, SqlTriggerConstants.PollingInterval * 1000);
            // Want to renew the leases faster than the lease time expires
            _renewLeasesTimer.Change(0, SqlTriggerConstants.LeaseTime * 1000 / 2);
        }

        /// <summary>
        /// Stops the watcher which stops polling for changes on the user's table
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync()
        {
            // There are three possibilities:
            // 1. We haven't started polling for changes yet. In that case, the first time the CheckForChangesCallback executes, the 
            // "_state == State.CheckingForChanges" if check will fail, and the method will skip directly to the finally clause, where it
            // registers the stopped state and disposes the timers
            // 2. We have started polling for changes, but are not processing any. The next time the CheckForChangesCallback executes,
            // the same steps will be follows as in 1
            // 3. We are currently processing changes. Once the CheckForChangesCallback finishes processing changes, it will reach the
            // finally clause, register the stopped state, and again dispose the timers
            Interlocked.Exchange(ref _state, Stopped);
            // Block until the timers have been released
            _signalStoppedHandle.WaitOne();
            _signalStoppedHandle.Dispose();
        }

        /// <summary>
        /// Executed once every "_leastTime" period. If the state of the watcher is <see cref="State.ProcessingChanges"/>, then 
        /// we will renew the leases held by the watcher on "_rows"
        /// </summary>
        /// <param name="state">Unused</param>
        private async void RenewLeasesCallback(object state)
        {
            await _renewLeasesLock.WaitAsync();
            try
            {
                if (_state == ProcessingChanges)
                {
                    // To prevent useless reinvocation of the callback while it's executing
                    _renewLeasesTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    await RenewLeasesAsync();
                }
            }
            finally
            {
                // Re-enable timer
                _renewLeasesTimer.Change(0, SqlTriggerConstants.LeaseTime * 1000  / 2);
                _renewLeasesLock.Release();
            }
        }

        /// <summary>
        /// Executed once every "_pollingInterval" period. If the state of the watcher is <see cref="State.CheckingForChanges"/>, then 
        /// the method query the change/worker tables for changes on the user's table. If any are found, the state of the watcher is
        /// transitioned to <see cref="State.ProcessingChanges"/> and the user's function is executed with the found changes. 
        /// If execution is successful, the leases on "_rows" are released and the state transitions to <see cref="State.CheckingForChanges"/>
        /// once more
        /// </summary>
        /// <param name="state"></param>
        private async void CheckForChangesCallback(object state)
        {
            await _checkForChangesLock.WaitAsync();
            try
            {
                if (_state == CheckingForChanges)
                {
                    // To prevent useless reinvocation of the callback while it's executing
                    _checkForChangesTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    await CheckForChangesAsync();
                    if (_rows.Count > 0)
                    {
                        // Even if StopAsync has been called at this point, we want to finishing processing this set of
                        // changes because we acquired leases on them in the CheckForChangesAsync call
                        Interlocked.Exchange(ref _state, ProcessingChanges);
                        var triggerValue = new ChangeTableData();
                        string whereCheck;
                        _queryStrings.TryGetValue(SqlTriggerConstants.WhereCheck, out whereCheck);
                        triggerValue.WorkerTableRows = _rows;
                        triggerValue.PrimaryKeys = _primaryKeys;
                        triggerValue.WhereCheck = whereCheck;
                        FunctionResult result = await _executor.TryExecuteAsync(new TriggeredFunctionData() { TriggerValue = triggerValue }, _cancellationTokenSource.Token);
                        if (result.Succeeded)
                        {
                            await ReleaseLeasesAsync();
                        }
                        else
                        {
                            //Should probably have some count for how many times we tried to execute the function. After a certain amount of times
                            // we should give up
                        }
                    }
                }
            }
            finally
            {
                if (_state == Stopped)
                {
                    await _checkForChangesTimer.DisposeAsync();
                    await _renewLeasesTimer.DisposeAsync();
                    // Signal that resources have been successfully released and timers stopped
                    _signalStoppedHandle.Set();
                }
                else
                {
                    // Re-enable timer
                    _checkForChangesTimer.Change(0, SqlTriggerConstants.PollingInterval * 1000);
                    _checkForChangesLock.Release();
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
            using (SqlConnection connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
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
        /// </summary>
        /// <returns></returns>
        private async Task GetPrimaryKeysAsync()
        {
            var getPrimaryKeysQuery =
                $"SELECT c.name, t.name\n" +
                $"FROM sys.indexes i\n" +
                $"INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id\n" +
                $"INNER JOIN sys.columns c ON ic.object_id = c.object_id AND c.column_id = ic.column_id\n" +
                $"INNER JOIN sys.types t ON c.user_type_id = t.user_type_id\n" +
                $"WHERE i.is_primary_key = 1 and i.object_id = OBJECT_ID(\'{_userTable}\');";

            using (SqlConnection connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
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
            }
        }

        /// <summary>
        /// Queries the change/worker tables to check for new changes on the user's table
        /// </summary>
        /// <returns></returns>
        private async Task CheckForChangesAsync()
        {
            using (SqlConnection connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
            {
                await connection.OpenAsync();
                // TODO: Set up locks on transaction
                SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead);

                using (SqlCommand getChangesCommand = BuildCheckForChangesCommand(connection, transaction))
                {
                    using (SqlDataReader reader = await getChangesCommand.ExecuteReaderAsync())
                    {
                        List<string> cols = new List<string>();
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
                        // Necessary so that other commands can re-use the parameters in the _primaryKeyValuesOfRows map
                        acquireLeaseCommand.Parameters.Clear();
                    }
                }
                await transaction.CommitAsync();
            }
        }

        /// <summary>
        /// Renews the leases held on _rows
        /// </summary>
        /// <returns></returns>
        private async Task RenewLeasesAsync()
        {
            using (SqlConnection connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
            {
                await connection.OpenAsync();
                foreach (var row in _rows)
                {
                    // Not great that we're doing a SqlCommand per row, should batch this
                    using (SqlCommand renewLeaseCommand = BuildRenewLeaseOnRowCommand(row, connection))
                    {
                        await renewLeaseCommand.ExecuteNonQueryAsync();
                        renewLeaseCommand.Parameters.Clear();
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
            await _renewLeasesLock.WaitAsync();
            try
            {
                using (SqlConnection connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
                {
                    await connection.OpenAsync();
                    SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead);
                    foreach (var row in _rows)
                    {
                        // Not great that we're doing a SqlCommand per row, should batch this
                        using (SqlCommand releaseLeaseCommand = BuildReleaseLeaseOnRowCommand(row, connection, transaction))
                        {
                            await releaseLeaseCommand.ExecuteNonQueryAsync();
                            releaseLeaseCommand.Parameters.Clear();
                        }
                    }
                    await transaction.CommitAsync();
                }
                _rows.Clear();
            }
            finally
            {
                // Want to do this before releasing the lock in case the renew leases timer goes off. It will see that
                // the state is checking for changes and not renew the (just released) leases
                // Only want to change the state if it was previously ProcessingChanges though. If it is stopped, for example,
                // we don't want to start polling for changes again
                Interlocked.CompareExchange(ref _state, CheckingForChanges, ProcessingChanges);
                _renewLeasesLock.Release();
            }
        }

        /// <summary>
        /// Builds the query to create the worker table if one does not already exist (<see cref="CreateWorkerTableAsync"/>)
        /// </summary>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private async Task<SqlCommand> BuildCreateTableCommandAsync(SqlConnection connection)
        {
            await GetPrimaryKeysAsync();

            var primaryKeysWithTypes = string.Empty;
            var primaryKeysList = string.Empty;
            foreach (var primaryKey in _primaryKeys.Keys)
            {
                string type;
                _primaryKeys.TryGetValue(primaryKey, out type);
                primaryKeysWithTypes += primaryKey + " " + type + ",\n";
                primaryKeysList += primaryKey + ", ";
            }
            // Remove the trailing ", "
            primaryKeysList = primaryKeysList.Substring(0, primaryKeysList.Length - 2);

            var createTableString = 
                $"IF OBJECT_ID(N\'{_workerTable}\', \'U\') IS NULL\n" +
                $"BEGIN\n" +
                $"CREATE TABLE {_workerTable} (\n" +
                $"{primaryKeysWithTypes}" +
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
            string primaryKeysInnerJoin;

            if (!_queryStrings.TryGetValue(SqlTriggerConstants.PrimaryKeysSelectList, out primaryKeysSelectList))
            {
                // If one isn't in the dictionary, neither is
                primaryKeysInnerJoin = string.Empty;
                primaryKeysSelectList = string.Empty;
                bool first = true;

                foreach (var key in _primaryKeys.Keys)
                {
                    primaryKeysSelectList += "c." + key + ", ";
                    if (!first)
                    {
                        primaryKeysInnerJoin += " AND ";
                    }
                    else
                    {
                        first = false;
                    }
                    primaryKeysInnerJoin += "c." + key + " = w." + key;
                }

                _queryStrings.Add(SqlTriggerConstants.PrimaryKeysSelectList, primaryKeysSelectList);
                _queryStrings.Add(SqlTriggerConstants.PrimaryKeysInnerJoin, primaryKeysInnerJoin);
            }
            else
            {
                _queryStrings.TryGetValue(SqlTriggerConstants.PrimaryKeysInnerJoin, out primaryKeysInnerJoin);
            }

            var getChangesQuery = 
                $"DECLARE @version bigint;\n" +
                $"SET @version = CHANGE_TRACKING_MIN_VALID_VERSION(OBJECT_ID(\'{_userTable}\'));\n" +
                $"SELECT TOP {SqlTriggerConstants.BatchSize} *\n" +
                $"FROM\n" +
                $"(SELECT {primaryKeysSelectList}c.SYS_CHANGE_VERSION, c.SYS_CHANGE_CREATION_VERSION, c.SYS_CHANGE_OPERATION, \n" +
                $"c.SYS_CHANGE_COLUMNS, c.SYS_CHANGE_CONTEXT, w.LeaseExpirationTime, w.DequeueCount, w.VersionNumber\n" +
                $"FROM CHANGETABLE (CHANGES {_userTable}, @version) AS c\n" +
                $"LEFT OUTER JOIN {_workerTable} AS w ON {primaryKeysInnerJoin}) AS Changes\n" +
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
            var whereCheck = string.Empty;
            var valuesList = string.Empty;
            bool first = true;

            foreach (var key in _primaryKeys.Keys)
            {
                string primaryKeyValue;
                row.TryGetValue(key, out primaryKeyValue);
                string parameterName = "@" + key;
                acquireLeaseCommand.Parameters.Add(new SqlParameter(parameterName, primaryKeyValue));

                if (!first)
                {
                    whereCheck += " AND ";
                }
                else
                {
                    first = false;
                }
                whereCheck += key + " = " + parameterName;
                valuesList += parameterName + ", ";
            }

            // Will already exist in the map after the first call to this method, i.e. after a row
            // already has a lease acquired on it
            _queryStrings.TryAdd(SqlTriggerConstants.WhereCheck, whereCheck);

            string versionNumber;
            row.TryGetValue("SYS_CHANGE_VERSION", out versionNumber);

            var acquireLeaseOnRow =
                $"IF NOT EXISTS (SELECT * FROM {_workerTable} WHERE {whereCheck})\n" +
                $"INSERT INTO {_workerTable}\n" +
                $"VALUES ({valuesList}DATEADD({SqlTriggerConstants.LeaseUnits}, {SqlTriggerConstants.LeaseTime}, SYSDATETIME()), 0, {versionNumber})\n" +
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
        /// Builds the query to renew leases on the rows in "_rows" (<see cref="RenewLeasesCallback(object)"/>)
        /// </summary>
        /// <param name="row">The row that the lease will be renewed on</param>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildRenewLeaseOnRowCommand(Dictionary<string, string> row, SqlConnection connection)
        {
            SqlCommand renewLeaseCommand = new SqlCommand();

            string whereCheck;
            _queryStrings.TryGetValue(SqlTriggerConstants.WhereCheck, out whereCheck);
            SqlBindingUtilities.AddPrimaryKeyParametersToCommand(renewLeaseCommand, row, _primaryKeys);

            var renewLeaseOnRow =
                $"UPDATE {_workerTable}\n" +
                $"SET LeaseExpirationTime = DATEADD({SqlTriggerConstants.LeaseUnits}, {SqlTriggerConstants.LeaseTime}, SYSDATETIME())\n" +
                $"WHERE {whereCheck};";

            renewLeaseCommand.CommandText = renewLeaseOnRow;
            renewLeaseCommand.Connection = connection;

            return renewLeaseCommand;
        }

        /// <summary>
        /// Builds the query to release leases on the rows in "_rows" after successful invocation of the user's function (<see cref="CheckForChangesCallback(object)"/>)
        /// </summary>
        /// <param name="row">The row that the lease will be released on</param>
        /// <param name="connection">The connection to add to the returned SqlCommand</param>
        /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildReleaseLeaseOnRowCommand(Dictionary<string, string> row, SqlConnection connection, SqlTransaction transaction)
        {
            SqlCommand releaseLeaseCommand = new SqlCommand();

            string whereCheck;
            _queryStrings.TryGetValue(SqlTriggerConstants.WhereCheck, out whereCheck);
            string versionNumber;
            row.TryGetValue("SYS_CHANGE_VERSION", out versionNumber);
            SqlBindingUtilities.AddPrimaryKeyParametersToCommand(releaseLeaseCommand, row, _primaryKeys);

            var releaseLeaseOnRow =
                $"DECLARE @current_version bigint;\n" +
                $"SET @current_version = \n" + 
                $"(SELECT VersionNumber\n" + 
                $"FROM {_workerTable}\n" + 
                $"WHERE {whereCheck});\n" +
                $"IF {versionNumber} > @current_version\n" +
                $"UPDATE {_workerTable}\n" +
                $"SET LeaseExpirationTime = NULL, DequeueCount = 0, VersionNumber = {versionNumber}\n" +
                $"WHERE {whereCheck};";

            releaseLeaseCommand.CommandText = releaseLeaseOnRow;
            releaseLeaseCommand.Connection = connection;
            releaseLeaseCommand.Transaction = transaction;

            return releaseLeaseCommand;
        }

        /// <summary>
        /// Returns the name of the worker table, removing the schema prefix from userTable if one exists
        /// </summary>
        /// <param name="userTable">The user table name the worker table name is based on</param>
        /// <returns>The worker table name</returns>
        private static string BuildWorkerTableName(string userTable)
        {
            string[] tableNameComponents = userTable.Split(new[] { '.' }, 2);
            // Don't want the schema name of the user table in the name of the worker table. If the user table is 
            // specified with a schema prefix, the result of the Split call will have the user table name stored
            // in the second index. If the result of the Split call doesn't have two elements, then the user table
            // wasn't prefixed with a schema so we can just use it directly
            if (tableNameComponents.Length == 2)
            {
                return SqlTriggerConstants.Schema + ".Worker_Table_" + tableNameComponents[1];
            }
            else
            {
                return SqlTriggerConstants.Schema + ".Worker_Table_" + userTable;
            }
        }
    }
}
