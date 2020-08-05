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
    internal class SqlTableWatcher
    {
        private readonly string _table;
        private readonly string _workerTable;
        private readonly string _connectionStringSetting;
        private readonly IConfiguration _configuration;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly CancellationTokenSource _cancellationTokenSource;

        // It should be impossible for multiple threads to access these at the same time because of the semaphores we use
        private readonly Dictionary<string, string> _primaryKeys;
        private readonly List<Dictionary<string, string>> _rows;
        private readonly Dictionary<string, string> _queryStrings;
        private readonly Dictionary<Dictionary<string, string>, string> _whereChecksOfRows;
        private readonly Dictionary<Dictionary<string, string>, string> _primaryKeyValuesOfRows;
        private readonly Timer _renewLeasesTimer;
        private readonly Timer _checkForChangesTimer;
        private readonly SemaphoreSlim _checkForChangesLock;
        private readonly SemaphoreSlim _renewLeasesLock;
        private State _state;

        private const int _batchSize = 10;
        private const int _maxDequeueCount = 5;
        // Unit of time is seconds
        private const string _leaseUnits = "s";
        private const int _leaseTime = 30 * 1000;
        // The minimal possible retention period is 1 minute. Is 10 seconds an acceptable polling time given that?
        private const int _pollingInterval = 10 * 1000;

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
        /// Thrown if any of the parameters are null
        /// </exception>
        public SqlTableWatcher(string table, string connectionStringSetting, IConfiguration configuration, ITriggeredFunctionExecutor executor)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _connectionStringSetting = connectionStringSetting ?? throw new ArgumentNullException(nameof(connectionStringSetting));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _workerTable = "Worker_Table_" + _table;

            _cancellationTokenSource = new CancellationTokenSource();
            _rows = new List<Dictionary<string, string>>();
            _queryStrings = new Dictionary<string, string>();
            _primaryKeys = new Dictionary<string, string>();
            _whereChecksOfRows = new Dictionary<Dictionary<string, string>, string>();
            _primaryKeyValuesOfRows = new Dictionary<Dictionary<string, string>, string>();

            _checkForChangesTimer = new Timer(CheckForChangesCallback);
            _renewLeasesTimer = new Timer(RenewLeasesCallback);
            // Should these be just normal semaphores?
            _checkForChangesLock = new SemaphoreSlim(1);
            _renewLeasesLock = new SemaphoreSlim(1);

        }

        /// <summary>
        /// Starts the watcher which begins polling for changes on the user's table specified in the constructor
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            await CreateWorkerTableAsync();
            _state = State.CheckingForChanges;
            _checkForChangesTimer.Change(0, _pollingInterval);
            _renewLeasesTimer.Change(0, _leaseTime);
        }

        /// <summary>
        /// Stops the watcher which stops polling for changes on the user's table
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync()
        {
            await _checkForChangesTimer.DisposeAsync();
            await _renewLeasesTimer.DisposeAsync();
        }

        /// <summary>
        /// Executed once every "_leastTime" period. If the state of the watcher is <see cref="State.ProcessingChanges"/>, then 
        /// we will renew the leases held by the watcher on "_rows"
        /// </summary>
        /// <param name="state">Unused </param>
        private async void RenewLeasesCallback(object state)
        {
            await _renewLeasesLock.WaitAsync();
            try
            {
                // Should I be using the state parameter instead? Is it unsafe otherwise because the other timer thread can modify this instance variable?
                if (_state == State.ProcessingChanges)
                {
                    // To prevent useless reinvocation of the callback while it's executing
                    _renewLeasesTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    await RenewLeasesAsync();
                }
            }
            finally
            {
                // Re-enable timer
                _renewLeasesTimer.Change(0, _leaseTime);
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
                if (_state == State.CheckingForChanges)
                {
                    // To prevent useless reinvocation of the callback while it's executing
                    _checkForChangesTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    await CheckForChangesAsync();
                    if (_rows.Count > 0)
                    {
                        _state = State.ProcessingChanges;
                        var triggerValue = new ChangeTableData();
                        triggerValue.workerTableRows = _rows;
                        triggerValue.whereChecks = _whereChecksOfRows;
                        var result = await _executor.TryExecuteAsync(new TriggeredFunctionData() { TriggerValue = triggerValue }, _cancellationTokenSource.Token);
                        if (result.Succeeded)
                        {
                            await ReleaseLeasesAsync();
                            _state = State.CheckingForChanges;
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
                // Re-enable timer
                _checkForChangesTimer.Change(0, _pollingInterval);
                _checkForChangesLock.Release();
            }   
        }

        /// <summary>
        /// Creates the worker table associated with the user's table, if one does not already exist
        /// </summary>
        /// <returns></returns>
        private async Task CreateWorkerTableAsync()
        {
            var createTableCommandString = await BuildCreateTableCommandStringAsync();
            
            // Should maybe change this so that we don't have to extract the connection string from the app setting
            // every time
            using (var connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
            {
                SqlCommand createTableCommand = new SqlCommand(createTableCommandString, connection);
                await connection.OpenAsync();
                await createTableCommand.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Retrieves the primary keys of the user's table and stores them in the "_primaryKeys" dictionary,
        /// which maps from primary key name to primary key type
        /// </summary>
        /// <returns></returns>
        private async Task GetPrimaryKeysAsync()
        {
            var getPrimaryKeysQuery = String.Format(
                "SELECT c.name, t.name\n" +
                "FROM sys.indexes i\n" +
                "INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id\n" +
                "INNER JOIN sys.columns c ON ic.object_id = c.object_id AND c.column_id = ic.column_id\n" +
                "INNER JOIN sys.types t ON c.user_type_id = t.user_type_id\n" +
                "WHERE i.is_primary_key = 1 and i.object_id = OBJECT_ID(\'{0}\');",
                _table
                );

            using (var connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
            {
                var getPrimaryKeysCommand = new SqlCommand(getPrimaryKeysQuery, connection);
                await connection.OpenAsync();
                using (var reader = await getPrimaryKeysCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        _primaryKeys.Add(reader.GetString(0), reader.GetString(1));
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
            using (var connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
            {
                await connection.OpenAsync();
                // TODO: Set up locks on transaction
                SqlTransaction transaction = connection.BeginTransaction();
                var getChangesCommand = new SqlCommand(BuildCheckForChangesString(), connection, transaction);
                using (var reader = await getChangesCommand.ExecuteReaderAsync())
                {
                    List<string> cols = new List<string>();
                    while (await reader.ReadAsync())
                    {
                        var row = SqlBindingUtilities.BuildDictionaryFromSqlRow(reader, cols);
                        _rows.Add(row);
                    }
                }

                foreach (var row in _rows)
                {
                    // Not great that we're doing a SqlCommand per row, should batch this
                    var acquireLeaseCommand = new SqlCommand(BuildAcquireLeaseOnRowString(row), connection, transaction);
                    await acquireLeaseCommand.ExecuteNonQueryAsync();
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
            using (var connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
            {
                await connection.OpenAsync();
                // TODO: Set up locks on transaction
                SqlTransaction transaction = connection.BeginTransaction();
                foreach (var row in _rows)
                {
                    // Not great that we're doing a SqlCommand per row, should batch this
                    var acquireLeaseCommand = new SqlCommand(BuildRenewLeaseOnRowString(row), connection, transaction);
                    await acquireLeaseCommand.ExecuteNonQueryAsync();
                }
                await transaction.CommitAsync();
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
                using (var connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
                {
                    await connection.OpenAsync();
                    // TODO: Set up locks on transaction
                    SqlTransaction transaction = connection.BeginTransaction();
                    foreach (var row in _rows)
                    {
                        // Not great that we're doing a SqlCommand per row, should batch this
                        var releaseLeaseCommand = new SqlCommand(BuildReleaseLeaseOnRowString(row), connection, transaction);
                        await releaseLeaseCommand.ExecuteNonQueryAsync();
                    }
                    await transaction.CommitAsync();
                }
                _rows.Clear();
                _whereChecksOfRows.Clear();
                _primaryKeyValuesOfRows.Clear();
            }
            finally
            {
                _renewLeasesLock.Release();
            }
        }

        /// <summary>
        /// Builds the query to create the worker table if one does not already exist (<see cref="CreateWorkerTableAsync"/>)
        /// </summary>
        /// <returns>The query</returns>
        private async Task<string> BuildCreateTableCommandStringAsync()
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
            _queryStrings.Add("primaryKeysList", primaryKeysList);
            // Remove the trailing ", "
            primaryKeysList = primaryKeysList.Substring(0, primaryKeysList.Length - 2);

            var createTableString = String.Format(
                "IF OBJECT_ID(N\'{0}\', \'U\') IS NULL\n" +
                "BEGIN\n" +
                "CREATE TABLE {0} (\n" +
                "{1}" +
                "LeaseExpirationTime datetime2,\n" +
                "DequeueCount int,\n" +
                "VersionNumber bigint\n" +
                "PRIMARY KEY({2})\n" +
                ");\n" +
                "END",
                _workerTable, primaryKeysWithTypes, primaryKeysList);
            return createTableString;
        }

        /// <summary>
        /// Builds the query to check for changes on the user's table (<see cref="CheckForChangesAsync"/>)
        /// </summary>
        /// <returns>The query</returns>
        private string BuildCheckForChangesString()
        {
            string primaryKeysSelectList;
            string primaryKeysInnerJoin;

            if (!_queryStrings.TryGetValue("primaryKeysSelectList", out primaryKeysSelectList))
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

                _queryStrings.Add("primaryKeysSelectList", primaryKeysSelectList);
                _queryStrings.Add("primaryKeysInnerJoin", primaryKeysInnerJoin);
            }
            else
            {
                _queryStrings.TryGetValue("primaryKeysInnerJoin", out primaryKeysInnerJoin);
            }

            var getChangesQuery = String.Format(
                "DECLARE @version bigint;\n" +
                "SET @version = CHANGE_TRACKING_MIN_VALID_VERSION(OBJECT_ID(\'{0}\'));\n" +
                "SELECT TOP {1} *\n" +
                "FROM\n" +
                "(SELECT {2}c.SYS_CHANGE_VERSION, c.SYS_CHANGE_CREATION_VERSION, c.SYS_CHANGE_OPERATION, \n" +
                "c.SYS_CHANGE_COLUMNS, c.SYS_CHANGE_CONTEXT, w.LeaseExpirationTime, w.DequeueCount, w.VersionNumber\n" +
                "FROM CHANGETABLE (CHANGES {0}, @version) AS c\n" +
                "LEFT OUTER JOIN {3} AS w ON {4}) AS Changes\n" +
                "WHERE (Changes.LeaseExpirationTime IS NULL AND\n" +
                "(Changes.VersionNumber IS NULL OR Changes.VersionNumber < Changes.SYS_CHANGE_VERSION)\n" +
                "OR Changes.LeaseExpirationTime < SYSDATETIME())\n" +
                "AND (Changes.DequeueCount IS NULL OR Changes.DequeueCount < {5})\n" +
                "ORDER BY Changes.SYS_CHANGE_VERSION ASC;\n",
                _table, _batchSize, primaryKeysSelectList, _workerTable, primaryKeysInnerJoin, _maxDequeueCount
                );

            return getChangesQuery;
        }

        /// <summary>
        /// Builds the query to acquire leases on the rows in "_rows" if changes are detected in the user's table (<see cref="CheckForChangesAsync"/>)
        /// </summary>
        /// <returns>The query</returns>
        private string BuildAcquireLeaseOnRowString(Dictionary<string, string> row)
        {
            var acquireLeaseOnRow =
                "IF NOT EXISTS (SELECT * FROM {0} WHERE {1})\n" +
                "INSERT INTO {0}\n" +
                "VALUES ({2}DATEADD({3}, {4}, SYSDATETIME()), 0, {5})\n" +
                "ELSE\n" +
                "UPDATE {0}\n" +
                "SET LeaseExpirationTime = DATEADD({3}, {4}, SYSDATETIME()), DequeueCount = DequeueCount + 1, VersionNumber = {5}\n" +
                "WHERE {1};";

            var whereCheck = string.Empty;
            var valuesList = string.Empty;
            bool first = true;

            foreach (var key in _primaryKeys.Keys)
            {
                string primaryKeyValue;
                row.TryGetValue(key, out primaryKeyValue);
                if (!first)
                {
                    whereCheck += " AND ";
                }
                else
                {
                    first = false;
                }
                whereCheck += key + " = " + primaryKeyValue;
                valuesList += primaryKeyValue + ", ";
            }

            _whereChecksOfRows.Add(row, whereCheck);
            _primaryKeyValuesOfRows.Add(row, valuesList);

            string versionNumber;
            row.TryGetValue("SYS_CHANGE_VERSION", out versionNumber);

            return String.Format(acquireLeaseOnRow, _workerTable, whereCheck, valuesList, _leaseUnits,
                _leaseTime, versionNumber);
        }

        /// <summary>
        /// Builds the query to renew leases on the rows in "_rows" (<see cref="RenewLeasesCallback(object)"/>)
        /// </summary>
        /// <returns>The query</returns>
        private string BuildRenewLeaseOnRowString(Dictionary<string, string> row)
        {
            var renewLeaseOnRow =
                "UPDATE {0}\n" +
                "SET LeaseExpirationTime = DATEADD({1}, {2}, SYSDATETIME())\n" +
                "WHERE {3};";
            string whereCheck;
            _whereChecksOfRows.TryGetValue(row, out whereCheck);
            return String.Format(renewLeaseOnRow, _workerTable, _leaseUnits, _leaseTime, whereCheck);
        }

        /// <summary>
        /// Builds the query to release leases on the rows in "_rows" after successful invocation of the user's function (<see cref="CheckForChangesCallback(object)"/>)
        /// </summary>
        /// <returns>The query</returns>
        private string BuildReleaseLeaseOnRowString(Dictionary<string, string> row)
        {
            var releaseLeaseOnRow =
                "UPDATE {0}\n" +
                "SET LeaseExpirationTime = NULL, DequeueCount = 0, VersionNumber = {1}\n" +
                "WHERE {2};";

            string whereCheck;
            _whereChecksOfRows.TryGetValue(row, out whereCheck);
            string versionNumber;
            row.TryGetValue("SYS_CHANGE_VERSION", out versionNumber);

            return String.Format(releaseLeaseOnRow, _workerTable, versionNumber, whereCheck);
        }

        /// <summary>
        /// Represents the current state of the watcher, which is either that it is currently polling for new changes (CheckingForChanges)
        /// or currently processing new changes that it found (ProcessingChanges)
        /// </summary>
        enum State
        {
            CheckingForChanges,
            ProcessingChanges
        }
    }
}
