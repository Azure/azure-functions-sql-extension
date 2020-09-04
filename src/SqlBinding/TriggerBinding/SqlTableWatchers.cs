// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SqlBinding.TriggerBinding;
using static SqlBinding.TriggerBinding.ScaleRecommendation;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    public class SqlTableWatchers
    {
        public class SqlPerformanceMonitor
        {
            private string _workerTable;
            private int _userTableID;
            private readonly string _globalStateTable;
            private readonly string _userTable;
            private readonly string _workerBatchSizesTable;
            private readonly string _connectionString;
            private Queue<long> _lastUnprocessedChanges;
            private string _leftOuterJoinWorkerTable;

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
            public SqlPerformanceMonitor(string table, string connectionString)
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
                _globalStateTable = $"[{SqlTriggerConstants.Schema}].[{SqlTriggerConstants.GlobalStateTable}]";
                _workerBatchSizesTable = $"[{SqlTriggerConstants.Schema}].[{SqlTriggerConstants.WorkerBatchSizesTable}]";
                _lastUnprocessedChanges = new Queue<long>();
            }

            /// <summary>
            /// Starts the performance monitor
            /// </summary>
            /// <returns></returns>
            public async Task StartAsync()
            {
                _userTableID = await GetUserTableIDAsync(_connectionString, _userTable);
                _workerTable = $"[{SqlTriggerConstants.Schema}].[Worker_Table_{_userTableID}]";
                await GetUserTablePrimaryKeysAsync();
            }

            /// <summary>
            /// Makes a scale recommendation based on the current number of unprocessed changes for the user table and worker count
            /// </summary>
            /// <param name="workerCount">The current number of workers processing changes for this user table</param>
            /// <param name="granularity">
            /// The granularity of the polling interval ("s" for seconds, "mi" for minutes, etc. <see cref="SqlTriggerConstants.ScaleControllerPollingIntervalUnits"/>)
            /// </param>
            /// <param name="pollingInterval">How often the this method is being called</param>
            /// <returns>
            /// A <see cref="SqlHeartbeat"/> containing the scale recommendation as well as additional metrics for this table
            /// </returns>
            public async Task<SqlHeartbeat> MakeScaleRecommendation(int workerCount, string granularity, long pollingInterval)
            {
                try
                {
                    SqlHeartbeat heartbeat;
                    long currentUnprocessedChanges = await GetCurrentUnprocessedChanges();
                    // Query to retrieve the changes somehow didn't return any results (no idea how this would happen, but just in case)
                    if (currentUnprocessedChanges == -1)
                    {
                        // Don't want to add this spurious value to _lastUnprocessedChanges, so immediately return
                        return new SqlHeartbeat(currentUnprocessedChanges,
                                new ScaleRecommendation(ScaleAction.None, keepWorkersAlive: true, reason: $"Unable to query for the metrics necessary to make a scale decision " +
                                $"for user table {_userTable}. Query to determine number of new changes in the past polling interval failed to return results."));
                    }

                    _lastUnprocessedChanges.Enqueue(currentUnprocessedChanges);
                    // There are unprocessed changes but no workers active to process them
                    if (workerCount == 0 && currentUnprocessedChanges > 0)
                    {
                        heartbeat = new SqlHeartbeat(currentUnprocessedChanges,
                            new ScaleRecommendation(ScaleAction.AddWorker, keepWorkersAlive: true, reason: $"There exist unprocessed changes for the user table {_userTable} but no " +
                            $" workers to process them. Adding a worker to start processing."));
                    }
                    // Just started gathering metrics, need to wait for more polling intervals to accumulate data
                    else if (_lastUnprocessedChanges.Count < SqlTriggerConstants.MinimumNumberOfSamples)
                    {
                        heartbeat = new SqlHeartbeat(currentUnprocessedChanges,
                                new ScaleRecommendation(ScaleAction.None, keepWorkersAlive: true, reason: $"Need to wait for" +
                                $" {SqlTriggerConstants.MinimumNumberOfSamples - _lastUnprocessedChanges.Count} more polling interval(s) to accumulate" +
                                $" sufficient metrics to make scale decisions for user table {_userTable}."));
                    }
                    else
                    {
                        // Remove the earliest samples if we have reached MinimumNumberOfSamples
                        while (_lastUnprocessedChanges.Count > SqlTriggerConstants.MinimumNumberOfSamples)
                        {
                            _lastUnprocessedChanges.Dequeue();
                        }

                        string lastUnprocessedChangesString = GenerateSamplesString();
                        var scaleRecommendationReason = $"The past {SqlTriggerConstants.MinimumNumberOfSamples} samples for the number of unprocessed changes are" +
                            $" {lastUnprocessedChangesString}.";
                        var pattern = GetPatternOfChange();

                        // The rate at which changes are being created is apparently starting to grow
                        if (pattern > 0)
                        {
                            double firstSample = _lastUnprocessedChanges.Peek();
                            double percentIncrease;
                            if (firstSample == 0)
                            {
                                // So the next if check passes
                                percentIncrease = SqlTriggerConstants.MinimumPercentIncrease;
                                scaleRecommendationReason += $" The number of unprocessed changes has gone from zero to the non-zero value {currentUnprocessedChanges}.";
                            }
                            else
                            {
                                percentIncrease = (currentUnprocessedChanges - firstSample) / firstSample * 100;
                                scaleRecommendationReason += $" The rate at which changes are being created has apparently increased by {percentIncrease,2}% for " +
                                    $"user table {_userTable}.";
                            }
                            if (percentIncrease >= SqlTriggerConstants.MinimumPercentIncrease)
                            {
                                scaleRecommendationReason += $" Since this represents an increase that is greater than or equal to the minimum of " +
                                    $"{SqlTriggerConstants.MinimumPercentIncrease}% required to add a worker, add a worker.";
                                heartbeat = new SqlHeartbeat(currentUnprocessedChanges,
                                    new ScaleRecommendation(ScaleAction.AddWorker, keepWorkersAlive: true, reason: scaleRecommendationReason));
                            }
                            else
                            {
                                scaleRecommendationReason += $" Since this is less than the minimum of {SqlTriggerConstants.MinimumPercentIncrease}% required to add a worker, do nothing.";
                                heartbeat = new SqlHeartbeat(currentUnprocessedChanges,
                                    new ScaleRecommendation(ScaleAction.None, keepWorkersAlive: true, reason: scaleRecommendationReason));
                            }
                        }

                        // The rate is decreasing
                        else if (pattern < 0)
                        {
                            double firstSample = _lastUnprocessedChanges.Peek();
                            var percentDecrease = (firstSample - currentUnprocessedChanges) / firstSample * 100;
                            scaleRecommendationReason += $" The rate at which changes are being created has apparently decreased by {percentDecrease,2}% for " +
                                $"the user table {_userTable}.";
                            if (percentDecrease >= SqlTriggerConstants.MinimumPercentDecrease)
                            {
                                scaleRecommendationReason += $" Since this represents a decrease that is greater than or equal to the minimum of " +
                                    $"{SqlTriggerConstants.MinimumPercentDecrease}% required to remove a worker, remove a worker.";
                                heartbeat = new SqlHeartbeat(currentUnprocessedChanges,
                                    new ScaleRecommendation(ScaleAction.RemoveWorker, keepWorkersAlive: true, reason: scaleRecommendationReason));
                            }
                            else
                            {
                                scaleRecommendationReason += $" Since this is less than the minimum of {SqlTriggerConstants.MinimumPercentIncrease}% required to remove a worker, do nothing.";
                                heartbeat = new SqlHeartbeat(currentUnprocessedChanges,
                                    new ScaleRecommendation(ScaleAction.None, keepWorkersAlive: true, reason: scaleRecommendationReason));
                            }
                        }

                        // The rate is neither strictly increasing or decreasing
                        else
                        {
                            scaleRecommendationReason += $" The rate at which changes are being created is neither strictly increasing or decreasing for user table {_userTable}.";
                            ScaleAction scaleAction = ScaleAction.None;
                            bool keepAlive = true;
                            // The number of unprocessed changes has been 0 for all past samples in the queue
                            if (_lastUnprocessedChanges.All(sample => sample == 0))
                            {
                                List<long> workerBatchSizes = await GetWorkerBatchSizes(granularity, pollingInterval);
                                // If the returned list contains -1, the worker batch sizes table hasn't been created yet, which presumably means
                                // no workers are alive and changes have yet to occur to the user's table. Do nothing in this case since there are no workers processing changes anyways
                                if (!workerBatchSizes.Contains(-1))
                                {
                                    scaleRecommendationReason += $" All past {SqlTriggerConstants.MinimumNumberOfSamples} samples have shown 0 unprocessed changes.";
                                    // There are less workers processing changes than there are workers alive
                                    if (workerBatchSizes.Count < workerCount)
                                    {
                                        scaleRecommendationReason += $" The number of workers processing changes is {workerBatchSizes.Count} but {workerCount} is/are alive. We can " +
                                             $"afford to remove at least one worker.";
                                        scaleAction = ScaleAction.RemoveWorker;
                                        // There is just one worker left and it didn't process any changes in the last polling interval, so we can afford not to keep it alive
                                        if (workerCount == 1)
                                        {
                                            keepAlive = false;
                                        }
                                    }
                                    else
                                    {
                                        var maxCapacity = workerBatchSizes.Count * SqlTriggerConstants.BatchSize;
                                        var sumBatchSizes = workerBatchSizes.Sum();
                                        scaleRecommendationReason += $" The average number of changes processed by workers is {sumBatchSizes} while the maximum capacity is {maxCapacity}.";
                                        // Workers are not processing full batch sizes of changes, and there is enough "wiggle room" to remove a worker
                                        if (maxCapacity - sumBatchSizes >= SqlTriggerConstants.BatchSize)
                                        {
                                            scaleRecommendationReason += $" Since the difference is at least the batch size of a worker, we can afford to remove a worker.";
                                            scaleAction = ScaleAction.RemoveWorker;
                                        }
                                        // Workers aren't processing full batch sizes of changes, but there isn't enough "wiggle room" to remove one
                                        else
                                        {
                                            scaleRecommendationReason += $" Since the difference is not at least the batch size of a worker, we cannot afford to remove a worker, " +
                                                $"so do nothing.";
                                        }
                                    }
                                }
                                else
                                {
                                    scaleRecommendationReason += " Changes have yet to start occurring to the user's table and there are no workers alive. Do nothing";
                                }
                            }

                            heartbeat = new SqlHeartbeat(currentUnprocessedChanges,
                                new ScaleRecommendation(scaleAction, keepWorkersAlive: keepAlive, reason: scaleRecommendationReason));
                        }
                    }
                    return heartbeat;
                }
                catch (Exception e)
                {
                    return new SqlHeartbeat(-1,
                        new ScaleRecommendation(ScaleAction.None, keepWorkersAlive: true, reason: $"Unable to query for the metrics necessary to make a scale decision for user table {_userTable}." +
                        $" Query to determine number of new changes in the past polling interval failed to return results with exception {e.Message}. {_leftOuterJoinWorkerTable}"));
                }
            }

            /// <summary>
            /// Returns a string representation of _lastUnprocessedChanges. The list of samples shows the oldest samples first, 
            /// newest samples last. 
            /// </summary>
            private string GenerateSamplesString()
            {
                var lastUnprocessedChangesString = string.Empty;
                bool first = true;
                foreach (var change in _lastUnprocessedChanges)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        lastUnprocessedChangesString += ", ";
                    }
                    lastUnprocessedChangesString += change;
                }
                return lastUnprocessedChangesString;
            }

            /// <summary>
            /// Returns 1 if the numbers in _lastUnprocessedChanges are strictly increasing, -1 if they're strictly decreasing, and 0 if neither is true
            /// </summary>
            private int GetPatternOfChange()
            {
                bool first = true;
                long lastSample = 0;
                var diffs = new List<long>();
                // Get the difference between sample i + 1 and i and store in diffs
                foreach (var sample in _lastUnprocessedChanges)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        diffs.Add(sample - lastSample);
                    }
                    lastSample = sample;
                }

                int pattern = 0;
                foreach (var diff in diffs)
                {
                    // Decreasing
                    if (diff < 0)
                    {
                        // Was previously increasing, return 0
                        if (pattern > 0)
                        {
                            return 0;
                        }
                        else
                        {
                            pattern = -1;
                        }
                    }
                    // Increasing
                    else if (diff > 0)
                    {
                        // Was previously decreasing, return 0
                        if (pattern < 0)
                        {
                            return 0;
                        }
                        else
                        {
                            pattern = 1;
                        }
                    }
                }
                return pattern;
            }

            /// <summary>
            /// Retrieves the primary keys of the user's table and uses them to build the _leftOuterJoinWorkerTable string
            /// </summary>
            /// <exception cref="InvalidOperationException">
            /// Thrown if no primary keys are found for the user table. This could be because the user table does not have
            /// any primary key columns.
            /// </exception>
            private async Task GetUserTablePrimaryKeysAsync()
            {
                var primaryKeys = new List<string>();
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var getUserTablePrimaryKeysCommand = BuildGetUserTablePrimaryKeysCommand(connection))
                    {
                        using (SqlDataReader reader = await getUserTablePrimaryKeysCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                primaryKeys.Add(reader.GetString(0));
                            }
                        }
                    }
                }

                if (primaryKeys.Count == 0)
                {
                    throw new InvalidOperationException($"Unable to determine the primary keys of user table {_userTable}. Potentially, the table does not have any primary " +
                        $"key columns. A primary key is required for every user table for which changes are being monitored.");
                }
                _leftOuterJoinWorkerTable = string.Join(" AND ", primaryKeys.Select(key => $"c.{key} = w.{key}"));
            }

            /// <summary>
            /// Returns the number of changes that have yet to be processed for this user table
            /// </summary>
            /// <returns>
            /// The number of changes, or -1 if the query to get the changes fails
            /// </returns>
            private async Task<long> GetCurrentUnprocessedChanges()
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    // If we want to avoid deadlocks, perhaps it's okay to make the transaction level ReadUncommitted
                    // We would get less accurate results, but we wouldn't be competing for table locks with other workers.
                    // Right now don't include a transaction since this command just involves reading data
                    using (SqlCommand getUnprocessedChangesCommand = BuildGetUnprocessedChangesCommand(connection))
                    {
                        using (SqlDataReader reader = await getUnprocessedChangesCommand.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return reader.GetInt64(0);
                            }
                        }
                    }   
                }
                return -1;
            }

            /// <summary>
            /// Returns a list of batch sizes for workers that have processed changes to this user table sometime in the
            /// last polling interval. If the worker batch sizes table hasn't been created yet, the list contains just the one 
            /// value -1
            /// </summary>
            /// <param name="granularity">The granularity of the polling interval ("s" for seconds, "mi" for minutes, etc.)</param>
            /// <param name="pollingInterval">How often the this method is being called</param>
            private async Task<List<long>> GetWorkerBatchSizes(string granularity, long pollingInterval)
            {
                var workerBatchSizes = new List<long>();
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    // If we want to avoid deadlocks, perhaps it's okay to make the transaction level ReadUncommitted
                    // We would get less accurate results, but we wouldn't be competing for table locks with other workers.
                    // Right now don't include a transaction since this command just involves reading data
                    using (SqlCommand getWorkerBatchSizesCommand = BuildGetWorkerBatchSizesCommand(connection, granularity, pollingInterval))
                    {
                        using (SqlDataReader reader = await getWorkerBatchSizesCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                workerBatchSizes.Add(reader.GetInt64(0));
                            }
                        }
                    }
                }
                return workerBatchSizes;
            }

            /// <summary>
            /// Builds the query to determine the names of the primary key columns of the user table
            /// </summary>
            /// <param name="connection">The connection to add to the returned SqlCommand</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildGetUserTablePrimaryKeysCommand(SqlConnection connection)
            {
                var getUserTablePrimaryKeysQuery =
                    $"SELECT c.name\n" +
                    $"FROM sys.indexes i\n" +
                    $"INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id\n" +
                    $"INNER JOIN sys.columns c ON ic.object_id = c.object_id AND c.column_id = ic.column_id\n" +
                    $"WHERE i.is_primary_key = 1 and i.object_id = OBJECT_ID(N\'{_userTable}\', \'U\');";

                return new SqlCommand(getUserTablePrimaryKeysQuery, connection);
            }

            /// <summary>
            /// Builds the query to get batch sizes of each worker that processed changes for this user table sometime in the last pollingInterval
            /// </summary>
            /// <param name="connection">The connection to add to the returned SqlCommand</param>
            /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
            /// <param name="granularity">The granularity of the polling interval ("s" for seconds, "mi" for minutes, etc.)</param>
            /// <param name="pollingInterval">How often the this method is being called</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildGetWorkerBatchSizesCommand(SqlConnection connection, string granularity, long pollingInterval)
            {
                var getWorkerBatchSizesQuery =
                    $"IF OBJECT_ID(N\'{_workerBatchSizesTable}\', \'U\') IS NOT NULL\n" +
                    $"SELECT BatchSize\n" +
                    $"FROM {_workerBatchSizesTable}\n" +
                    $"WHERE UserTableID = {_userTableID} AND DATEADD({granularity}, -{pollingInterval}, SYSDATETIME()) <= Timestamp;\n" +
                    $"ELSE\n" +
                    $"SELECT -1;";

                return new SqlCommand(getWorkerBatchSizesQuery, connection);
            }

            /// <summary>
            /// Builds the query to check for how many unprocessed changes currently exist for the user's table
            /// If the worker tables have been created, mimics the query the workers use to get unprocessed changes
            /// Otherwise, queries the change table with the minimum valid version number (which is essentially what the very 
            /// first worker to start processing changes would have to do anyway)
            /// </summary>
            /// <param name="connection">The connection to add to the returned SqlCommand</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildGetUnprocessedChangesCommand(SqlConnection connection)
            {
                // COUNT_BIG returns a bigint, which is composed of 8 bytes, not 4, in the case that there are a lot of unprocessed changes
                // The worker table is created after the global state table is created/populated, so if it exists, so too does info. in the global state table
                var getChangesQuery =
                    $"DECLARE @version bigint;\n" +
                    $"IF OBJECT_ID(N\'{_workerTable}\', \'U\') IS NULL\n" +
                    $"BEGIN\n" +
                    $"SET @version = CHANGE_TRACKING_MIN_VALID_VERSION({_userTableID});\n" +
                    $"SELECT COUNT_BIG(*)\n" +
                    $"FROM CHANGETABLE (CHANGES {_userTable}, @version) AS c\n" +
                    $"END\n" +
                    $"ELSE\n" +
                    $"BEGIN\n" +
                    $"SELECT @version = GlobalVersionNumber FROM {_globalStateTable} WHERE UserTableID = {_userTableID}\n" +
                    $"SELECT COUNT_BIG(*)\n" +
                    $"FROM\n" +
                    $"(SELECT c.SYS_CHANGE_VERSION, w.LeaseExpirationTime, w.DequeueCount, w.VersionNumber\n" +
                    $"FROM CHANGETABLE (CHANGES {_userTable}, @version) AS c\n" +
                    $"LEFT OUTER JOIN {_workerTable} AS w ON {_leftOuterJoinWorkerTable}) AS CHANGES\n" +
                    $"WHERE (Changes.LeaseExpirationTime IS NULL AND\n" +
                    $"(Changes.VersionNumber IS NULL OR Changes.VersionNumber < Changes.SYS_CHANGE_VERSION)\n" +
                    $"OR Changes.LeaseExpirationTime < SYSDATETIME())\n" +
                    $"AND (Changes.DequeueCount IS NULL OR Changes.DequeueCount < {SqlTriggerConstants.MaxDequeueCount})\n" +
                    $"END";

                return new SqlCommand(getChangesQuery, connection);
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
            private static string[] variableLengthTypes = new string[] { "varchar", "nvarchar", "nchar", "char", "binary", "varbinary" };
            private static string[] variablePrecisionTypes = new string[] { "numeric", "decimal" };

            private readonly string _workerID;
            private string _workerTable;
            private int _userTableID;
            private readonly string _globalStateTable;
            private readonly string _workerBatchSizesTable;
            private readonly string _userTable;
            private readonly string _connectionString;
            private readonly ITriggeredFunctionExecutor _executor;
            private readonly ILogger _logger;
            private CancellationTokenSource _cancellationTokenSourceExecutor;
            private readonly CancellationTokenSource _cancellationTokenSourceCheckForChanges;
            private readonly CancellationTokenSource _cancellationTokenSourceRenewLeases;
            private readonly CancellationTokenSource _cancellationTokenSourceCleanupWorkerBatchSizesTable;

            // It should be impossible for multiple threads to access these at the same time because of the semaphore we use
            private readonly List<Dictionary<string, string>> _rows;
            private readonly List<string> _userTableColumns;
            private readonly List<string> _whereChecks;
            private readonly Dictionary<string, string> _primaryKeys;
            private readonly Dictionary<string, Lazy<string>> _queryStrings;

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
                _globalStateTable = $"[{SqlTriggerConstants.Schema}].[{SqlTriggerConstants.GlobalStateTable}]";
                _workerBatchSizesTable = $"[{SqlTriggerConstants.Schema}].[{SqlTriggerConstants.WorkerBatchSizesTable}]";
                // For now use the machine name as the worker ID
                _workerID = Environment.MachineName;

                _cancellationTokenSourceExecutor = new CancellationTokenSource();
                _cancellationTokenSourceCheckForChanges = new CancellationTokenSource();
                _cancellationTokenSourceRenewLeases = new CancellationTokenSource();
                _cancellationTokenSourceCleanupWorkerBatchSizesTable = new CancellationTokenSource();
                _rowsLock = new SemaphoreSlim(1);

                _rows = new List<Dictionary<string, string>>();
                _userTableColumns = new List<string>();
                _whereChecks = new List<string>();
                _queryStrings = new Dictionary<string, Lazy<string>>();
                _primaryKeys = new Dictionary<string, string>();

                LazyInitializeQueryStrings();
            }

            /// <summary>
            /// Starts the watcher which begins polling for changes on the user's table specified in the constructor
            /// </summary>
            /// <returns></returns>
            public async Task StartAsync()
            {
                await CreateWorkerTablesAsync();

                #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Task.Run(() =>
                {
                    CheckForChangesAsync(_cancellationTokenSourceCheckForChanges.Token);
                    RenewLeasesAsync(_cancellationTokenSourceRenewLeases.Token);
                    CleanUpWorkerBatchSizesTableAsync(_cancellationTokenSourceCleanupWorkerBatchSizesTable.Token);
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
                _cancellationTokenSourceCleanupWorkerBatchSizesTable.Cancel();
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
                        await Task.Delay(SqlTriggerConstants.LeaseInterval / 2 * 1000, token);
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
            /// Executed once every <see cref="SqlTriggerConstants.CleanupInterval"/> period.
            /// When executed, removes stale rows from _workerBatchSizesTable. A stale row is one that hasn't been updated
            /// since the last time this method executed
            /// </summary>
            /// <param name="token">
            /// If the token is cancelled, the thread stops cleaning up the worker batch sizes table and removes the row
            /// corresponding to this _workerID and _userTableID from the worker batch sizes table
            /// </param>
            private async Task CleanUpWorkerBatchSizesTableAsync(CancellationToken token)
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        await CleanUpWorkerBatchSizesTableAsync();
                        // The Delay will exit if the token is cancelled
                        await Task.Delay(SqlTriggerConstants.CleanupInterval * 1000, token);
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
                    try
                    {
                        // Once this worker shuts down, remove its data from the worker batch sizes table
                        await DeleteRowFromWorkerBatchSizesTableAsync();
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning($"Unable to delete row corresponding to the worker ID {_workerID} and user table {_userTable} from the worker batch sizes table" +
                            $" due to error {e.Message}");
                    }
                    _cancellationTokenSourceCleanupWorkerBatchSizesTable.Dispose();
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
                await GetUserTableSchemaAsync();
                _workerTable = $"[{SqlTriggerConstants.Schema}].[Worker_Table_{_userTableID}]";

                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
                    {
                        // Create the schema where the worker tables will be located if it does not already exist
                        using (SqlCommand createSchemaCommand = BuildCreateSchemaCommand(connection, transaction))
                        {
                            await createSchemaCommand.ExecuteNonQueryAsync();
                        }

                        // Create the global state table, if one doesn't already exist for this database
                        using (SqlCommand createGlobalStateTableCommand = BuildCreateGlobalStateTableCommand(connection, transaction))
                        {
                            await createGlobalStateTableCommand.ExecuteNonQueryAsync();
                        }

                        // Insert a row into the global state table for this user table, if one doesn't already exist
                        using (SqlCommand insertRowGlobalStateTableCommand = BuildInsertRowGlobalStateTableCommand(connection, transaction))
                        {
                            try
                            {
                                await insertRowGlobalStateTableCommand.ExecuteNonQueryAsync();
                            }
                            // Could fail if we try to insert a NULL value into the GlobalVersionNumber, which happens when CHANGE_TRACKING_MIN_VALID_VERSION 
                            // returns NULL for the user table, meaning that change tracking is not enabled for either the database or table (or both)
                            catch (Exception e)
                            {
                                var errorMessage = $"Failed to start processing changes to table {_userTable}, potentially because change tracking was not " +
                                    $"enabled for the table or database {connection.Database}.";
                                _logger.LogWarning(errorMessage + $" Exact exception thrown is {e.Message}");
                                throw new InvalidOperationException(errorMessage);
                            }
                        }

                        // Create the worker table, if one doesn't already exist for this user table
                        using (SqlCommand createWorkerTableCommand = BuildCreateWorkerTableCommand(connection, transaction))
                        {
                            await createWorkerTableCommand.ExecuteNonQueryAsync();
                        }

                        // Create the worker batch size table, if one doesn't already exist for this user table
                        using (SqlCommand createWorkerBatchSizeTableCommand = BuildCreateWorkerBatchSizesTableCommand(connection, transaction))
                        {
                            await createWorkerBatchSizeTableCommand.ExecuteNonQueryAsync();
                        }

                        // Upsert a row for this worker/user table with a batch size of 0 to indicate that this worker is now live and able to process changes
                        using (SqlCommand upsertRowWorkerBatchSizeTableCommand = BuildUpsertRowWorkerBatchSizesTableCommand(connection, transaction, 0))
                        {
                            await upsertRowWorkerBatchSizeTableCommand.ExecuteNonQueryAsync();
                        }

                        await transaction.CommitAsync();
                    }
                }
            }

            /// <summary>
            /// Retrieves the primary keys of the user's table and stores them in the _primaryKeys dictionary,
            /// which maps from primary key name to primary key type
            /// Also retrieves the column names of the user's table and stores them in _userTableColumns,
            /// as well as the user table's OBJECT_ID which it stores to _userTableID
            /// </summary>
            /// <exception cref="InvalidOperationException">
            /// Thrown if the query to retrieve the OBJECT_ID of the user table fails to correctly execute
            /// This can happen if the OBJECT_ID call returns NULL, meaning that the user table might not exist in the database
            /// </exception>
            private async Task GetUserTableSchemaAsync()
            {
                // Necessary in the case that a prior attempt to start the SqlTableWatcher failed.
                // Could be the case that these were partially populated, so should clear and repopulate them
                _primaryKeys.Clear();

                using (var connection = new SqlConnection(_connectionString))
                {
                    // I don't think I need a transaction for this since the command just reads data
                    await connection.OpenAsync();
                    // Determine the primary keys of the user table
                    using (var getPrimaryKeysCommand = BuildGetUserTablePrimaryKeysCommand(connection))
                    {
                        using (SqlDataReader reader = await getPrimaryKeysCommand.ExecuteReaderAsync())
                        {
                            await DeterminePrimaryKeyTypesAsync(reader);
                        }
                    }

                    _userTableColumns.Clear();
                    // Determine the names of the user table columns
                    using (var getColumnNamesCommand = BuildGetUserTableColumnNamesCommand(connection))
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
                _userTableID = await GetUserTableIDAsync(_connectionString, _userTable);
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
                            using (SqlCommand updateGlobalVersionNumberCommand = BuildUpdateGlobalVersionNumberCommand(connection, transaction))
                            {
                                await updateGlobalVersionNumberCommand.ExecuteNonQueryAsync();
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
            /// Removes stale rows from _workerBatchSizesTable. A stale row is one that hasn't been updated
            /// since the last time the cleanup task ran
            /// </summary>
            private async Task CleanUpWorkerBatchSizesTableAsync()
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    // I don't think I need a transaction for just deleting rows from the worker batch sizes table
                    // Even if we read the timestamp of a row, determine its stale, then another worker actually updates the timestamp,
                    // but we delete the row anyway, there's functionality in place to deal with that
                    using (SqlCommand cleanUpWorkerBatchSizesTable = BuildCleanUpWorkerBatchSizesTableCommand(connection))
                    {
                        await cleanUpWorkerBatchSizesTable.ExecuteNonQueryAsync();
                    }
                }
            }

            /// <summary>
            /// Removes the row corresponding to this _workerID and _userTable from the worker batch sizes table
            /// </summary>
            private async Task DeleteRowFromWorkerBatchSizesTableAsync()
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    // I don't think I need a transaction for deleting the row corresponding to this worker from the batch sizes table either
                    using (SqlCommand deleteRowFromWorkerBatchSizesTable = BuildDeleteRowFromWorkerBatchSizesTableCommand(connection))
                    {
                        await deleteRowFromWorkerBatchSizesTable.ExecuteNonQueryAsync();
                    }
                }
            }

            /// <summary>
            /// Renews the leases held on _rows
            /// </summary>
            private async Task RenewLeasesAsync()
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    // I don't think I need a transaction for renewing leases. If this worker reads in a row from the worker table
                    // and determines that it corresponds to its batch of changes, but then that row gets deleted by a cleanup task,
                    // it shouldn't renew its lease on it anyways
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
                        using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
                        {
                            // Release the leases held on _rows
                            using (SqlCommand releaseLeaseCommand = BuildReleaseLeasesCommand(connection, transaction))
                            {
                                await releaseLeaseCommand.ExecuteNonQueryAsync();
                            }
                            // Update the batch size of this worker and _userTable to be the most recently processed batch size
                            using (SqlCommand upsertRowWorkerBatchSizesTableCommand = BuildUpsertRowWorkerBatchSizesTableCommand(connection, transaction, _rows.Count))
                            {
                                await upsertRowWorkerBatchSizesTableCommand.ExecuteNonQueryAsync();
                            }
                            // Update the global state table if we have processed all changes with version number <= newVersionNumber, and clean up the worker table
                            // to remove all rows with VersionNumber <= newVersionNumber
                            using (SqlCommand updateGlobalStateTableCommand = BuildUpdateGlobalStateTableCommand(connection, transaction, newVersionNumber, _rows.Count))
                            {
                                await updateGlobalStateTableCommand.ExecuteNonQueryAsync();
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

            /// <summary>
            /// Builds the command to determine the primary key column names and types of the user table
            /// </summary>
            /// <param name="connection">The connection to add to the returned SqlCommand</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildGetUserTablePrimaryKeysCommand(SqlConnection connection)
            {
                var getUserTablePrimaryKeysQuery =
                    $"SELECT c.name, t.name, c.max_length, c.precision, c.scale\n" +
                    $"FROM sys.indexes i\n" +
                    $"INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id\n" +
                    $"INNER JOIN sys.columns c ON ic.object_id = c.object_id AND c.column_id = ic.column_id\n" +
                    $"INNER JOIN sys.types t ON c.user_type_id = t.user_type_id\n" +
                    $"WHERE i.is_primary_key = 1 and i.object_id = OBJECT_ID(N\'{_userTable}\', \'U\');";

                return new SqlCommand(getUserTablePrimaryKeysQuery, connection);
            }

            /// <summary>
            /// Builds the command to determine the names of the user table columns
            /// </summary>
            /// <param name="connection">The connection to add to the returned SqlCommand</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildGetUserTableColumnNamesCommand(SqlConnection connection)
            {
                var getUserTableColumnNamesQuery =
                    $"SELECT name\n" +
                    $"FROM sys.columns\n" +
                    $"WHERE object_id = OBJECT_ID(\'{_userTable}\');";

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
                var updateGlobalStateTableCommand =
                    $"DECLARE @min_version bigint;\n" +
                    $"DECLARE @current_version bigint;\n" +
                    $"SET @min_version = CHANGE_TRACKING_MIN_VALID_VERSION({_userTableID});\n" +
                    $"SELECT @current_version = GlobalVersionNumber FROM {_globalStateTable} WHERE UserTableID = {_userTableID};\n" +
                    $"IF @current_version < @min_version\n" +
                    $"UPDATE {_globalStateTable}\n" +
                    $"SET GlobalVersionNumber = @min_version\n" +
                    $"WHERE UserTableID = {_userTableID};";

                return new SqlCommand(updateGlobalStateTableCommand, connection, transaction);
            }

            /// <summary>
            /// Builds the query to check for changes on the user's table (<see cref="CheckForChangesAsync"/>)
            /// </summary>
            /// <param name="connection">The connection to add to the returned SqlCommand</param>
            /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildCheckForChangesCommand(SqlConnection connection, SqlTransaction transaction)
            {

                _queryStrings.TryGetValue(SqlTriggerConstants.PrimaryKeysSelectList, out Lazy<string> primaryKeysSelectList);
                _queryStrings.TryGetValue(SqlTriggerConstants.LeftOuterJoinWorkerTable, out Lazy<string> leftOuterJoinWorkerTable);
                _queryStrings.TryGetValue(SqlTriggerConstants.LeftOuterJoinUserTable, out Lazy<string> leftOuterJoinUserTable);
                _queryStrings.TryGetValue(SqlTriggerConstants.UserTableColumnsSelectList, out Lazy<string> userTableColumnsSelectList);

                var getChangesQuery =
                    $"DECLARE @version bigint;\n" +
                    $"SELECT @version = GlobalVersionNumber FROM {_globalStateTable} WHERE UserTableID = {_userTableID};\n" +
                    $"SELECT TOP {SqlTriggerConstants.BatchSize} *\n" +
                    $"FROM\n" +
                    $"(SELECT {primaryKeysSelectList.Value}, {userTableColumnsSelectList.Value}c.SYS_CHANGE_VERSION, c.SYS_CHANGE_CREATION_VERSION, c.SYS_CHANGE_OPERATION, \n" +
                    $"c.SYS_CHANGE_COLUMNS, c.SYS_CHANGE_CONTEXT, w.LeaseExpirationTime, w.DequeueCount, w.VersionNumber\n" +
                    $"FROM CHANGETABLE (CHANGES {_userTable}, @version) AS c\n" +
                    $"LEFT OUTER JOIN {_workerTable} AS w ON {leftOuterJoinWorkerTable.Value}\n" +
                    $"LEFT OUTER JOIN {_userTable} AS u ON {leftOuterJoinUserTable.Value}) AS CHANGES\n" +
                    $"WHERE (Changes.LeaseExpirationTime IS NULL AND\n" +
                    $"(Changes.VersionNumber IS NULL OR Changes.VersionNumber < Changes.SYS_CHANGE_VERSION)\n" +
                    $"OR Changes.LeaseExpirationTime < SYSDATETIME())\n" +
                    $"AND (Changes.DequeueCount IS NULL OR Changes.DequeueCount < {SqlTriggerConstants.MaxDequeueCount})\n" +
                    $"ORDER BY Changes.SYS_CHANGE_VERSION ASC;\n";

                return new SqlCommand(getChangesQuery, connection, transaction);
            }

            /// <summary>
            /// Builds the command to delete stale rows from the _workerBatchSizesTable (<see cref="CleanUpWorkerBatchSizesTableAsync"/>)
            /// </summary>
            /// <param name="connection">The connection to add to the returned SqlCommand</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildCleanUpWorkerBatchSizesTableCommand(SqlConnection connection)
            {
                var cleanUpWorkerBatchSizesTableCommand =
                    $"DELETE FROM {_workerBatchSizesTable}\n" +
                    $"WHERE DATEADD({SqlTriggerConstants.CleanupUnits}, {SqlTriggerConstants.CleanupInterval}, Timestamp) < SYSDATETIME();";

                return new SqlCommand(cleanUpWorkerBatchSizesTableCommand, connection);
            }

            /// <summary>
            /// Builds the command to delete this worker's row from the worker batch sizes table (<see cref="DeleteRowFromWorkerBatchSizesTableAsync"/>)
            /// </summary>
            /// <param name="connection">The connection to add to the returned SqlCommand</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildDeleteRowFromWorkerBatchSizesTableCommand(SqlConnection connection)
            {
                var cleanUpWorkerBatchSizesTableCommand =
                    $"DELETE FROM {_workerBatchSizesTable}\n" +
                    $"WHERE UserTableID = {_userTableID} AND WorkerID = \'{_workerID}\'";

                return new SqlCommand(cleanUpWorkerBatchSizesTableCommand, connection);
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
                var acquireLeasesCommandString = string.Empty;
                var index = 0;

                foreach (var row in _rows)
                {
                    var whereCheck = string.Join(" AND ", _primaryKeys.Keys.Select(key => $"{key} = @{key}_{index}"));
                    var valuesList = string.Join(", ", _primaryKeys.Keys.Select(key => $"@{key}_{index}"));
                    _whereChecks.Add(whereCheck);

                    row.TryGetValue("SYS_CHANGE_VERSION", out string versionNumber);
                    acquireLeasesCommandString +=
                        $"IF NOT EXISTS (SELECT * FROM {_workerTable} WHERE {whereCheck})\n" +
                        $"INSERT INTO {_workerTable}\n" +
                        $"VALUES ({valuesList}, DATEADD({SqlTriggerConstants.LeaseUnits}, {SqlTriggerConstants.LeaseInterval}, SYSDATETIME()), 0, {versionNumber})\n" +
                        $"ELSE\n" +
                        $"UPDATE {_workerTable}\n" +
                        $"SET LeaseExpirationTime = DATEADD({SqlTriggerConstants.LeaseUnits}, {SqlTriggerConstants.LeaseInterval}, SYSDATETIME()), DequeueCount = DequeueCount + 1, " +
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
                SqlCommand renewLeasesCommand = new SqlCommand();
                SqlBindingUtilities.AddPrimaryKeyParametersToCommand(renewLeasesCommand, _rows, _primaryKeys.Keys);
                var renewLeasesCommandString = string.Empty;
                var index = 0;

                foreach (var row in _rows)
                {
                    renewLeasesCommandString +=
                    $"UPDATE {_workerTable}\n" +
                    $"SET LeaseExpirationTime = DATEADD({SqlTriggerConstants.LeaseUnits}, {SqlTriggerConstants.LeaseInterval}, SYSDATETIME())\n" +
                    $"WHERE {_whereChecks.ElementAt(index++)};\n";
                }

                renewLeasesCommand.CommandText = renewLeasesCommandString;
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
                var releaseLeasesCommandString = $"DECLARE @current_version bigint;\n";
                SqlBindingUtilities.AddPrimaryKeyParametersToCommand(releaseLeasesCommand, _rows, _primaryKeys.Keys);
                var index = 0;

                foreach (var row in _rows)
                {
                    var whereCheck = _whereChecks.ElementAt(index++);
                    row.TryGetValue("SYS_CHANGE_VERSION", out string versionNumber);

                    releaseLeasesCommandString +=
                        $"SELECT @current_version = VersionNumber\n" +
                        $"FROM {_workerTable}\n" +
                        $"WHERE {whereCheck};\n" +
                        $"IF {versionNumber} >= @current_version\n" +
                        $"UPDATE {_workerTable}\n" +
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

                string primaryKeysWithTypes = string.Join(",\n", _primaryKeys.Select(pair => $"{pair.Key} {pair.Value}"));
                string primaryKeysList = string.Join(", ", _primaryKeys.Keys);

                var createWorkerTableCommand =
                    $"IF OBJECT_ID(N\'{_workerTable}\', \'U\') IS NULL\n" +
                    $"CREATE TABLE {_workerTable} (\n" +
                    $"{primaryKeysWithTypes},\n" +
                    $"LeaseExpirationTime datetime2,\n" +
                    $"DequeueCount int,\n" +
                    $"VersionNumber bigint\n" +
                    $"PRIMARY KEY({primaryKeysList})\n" +
                    $");\n";

                return new SqlCommand(createWorkerTableCommand, connection, transaction);
            }

            /// <summary>
            /// Builds the command to create the worker batch size table if one does not already exist
            /// </summary>
            /// <param name="connection">The connection to attach to the returned SqlCommand</param>
            /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildCreateWorkerBatchSizesTableCommand(SqlConnection connection, SqlTransaction transaction)
            {
                var createWorkerBatchSizeTableCommand =
                    $"IF OBJECT_ID(N\'{_workerBatchSizesTable}\', \'U\') IS NULL\n" +
                    $"CREATE TABLE {_workerBatchSizesTable} (\n" +
                    $"UserTableID int,\n" +
                    $"WorkerID varchar(100),\n" +
                    $"BatchSize bigint,\n" +
                    $"Timestamp datetime2\n" +
                    $"PRIMARY KEY(UserTableID, WorkerID)\n" +
                    $");";
                return new SqlCommand(createWorkerBatchSizeTableCommand, connection, transaction);
            }

            /// <summary>
            /// Builds the command to upsert a row into the worker batch sizes table (insert one if one does not already exist for this user table/workerID,
            /// otherwise update the existing row to have BatchSize = batchSize)
            /// </summary>
            /// <param name="connection">The connection to attach to the returned SqlCommand</param>
            /// <param name="transaction">The transaction to attached to the returned SqlCommand</param>
            /// <param name="batchSize">The most recently processed batch size of this worker</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildUpsertRowWorkerBatchSizesTableCommand(SqlConnection connection, SqlTransaction transaction, long batchSize)
            {
                var insertRowWorkerBatchSizeTableCommand =
                    $"IF NOT EXISTS (SELECT * FROM {_workerBatchSizesTable} WHERE UserTableID = {_userTableID} AND WorkerID = \'{_workerID}\')\n" +
                    $"INSERT INTO {_workerBatchSizesTable}\n" +
                    $"VALUES ({_userTableID}, \'{_workerID}\', {batchSize}, SYSDATETIME());\n" +
                    $"ELSE\n" +
                    $"UPDATE {_workerBatchSizesTable}\n" +
                    $"SET BatchSize = {batchSize}, Timestamp = SYSDATETIME()\n" +
                    $"WHERE UserTableID = {_userTableID} AND WorkerID = \'{_workerID}\';";

                return new SqlCommand(insertRowWorkerBatchSizeTableCommand, connection, transaction);
            }

            /// <summary>
            /// Builds the command to create the schema where the worker tables are located if it does not already exist (<see cref="CreateWorkerTablesAsync"/>)
            /// </summary>
            /// <param name="connection">The connection to attach to the returned SqlCommand</param>
            /// <param name="transaction">The transaction to add to the returned SqlCommand</param>
            /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
            private SqlCommand BuildCreateSchemaCommand(SqlConnection connection, SqlTransaction transaction)
            {
                var createSchemaCommand =
                    $"IF SCHEMA_ID(N\'{SqlTriggerConstants.Schema}\') IS NULL\n" +
                    $"EXEC (\'CREATE SCHEMA {SqlTriggerConstants.Schema}\')";

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
                var createGlobalStateTableCommand =
                    $"IF OBJECT_ID(N\'{_globalStateTable}\', \'U\') IS NULL\n" +
                    $"CREATE TABLE {_globalStateTable} (\n" +
                    $"UserTableID int PRIMARY KEY,\n" +
                    $"GlobalVersionNumber bigint NOT NULL\n" +
                    $");";
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
                var insertRowGlobalStateTableCommand =
                    $"IF NOT EXISTS (SELECT * FROM {_globalStateTable} WHERE UserTableID = {_userTableID})\n" +
                    $"INSERT INTO {_globalStateTable}\n" +
                    $"VALUES ({_userTableID}, CHANGE_TRACKING_MIN_VALID_VERSION({_userTableID}));\n";

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
            private SqlCommand BuildUpdateGlobalStateTableCommand(SqlConnection connection, SqlTransaction transaction, long newVersionNumber, long rowsProcessed)
            {
                _queryStrings.TryGetValue(SqlTriggerConstants.LeftOuterJoinWorkerTable, out Lazy<string> leftOuterJoin);

                var updateGlobalStateTableCommand =
                    $"DECLARE @current_version bigint;\n" +
                    $"DECLARE @unprocessed_changes bigint;\n" +
                    $"SELECT @current_version = GlobalVersionNumber FROM {_globalStateTable} WHERE UserTableID = {_userTableID};\n" +
                    $"SELECT @unprocessed_changes = \n" +
                    $"COUNT(*)\n" +
                    $"FROM\n" +
                    $"(SELECT c.SYS_CHANGE_VERSION FROM CHANGETABLE(CHANGES {_userTable}, @current_version) AS c\n" +
                    $"LEFT OUTER JOIN {_workerTable} AS w ON {leftOuterJoin.Value}\n" +
                    $"WHERE c.SYS_CHANGE_VERSION <= {newVersionNumber}\n" +
                    $"AND ((w.VersionNumber IS NULL OR w.VersionNumber != c.SYS_CHANGE_VERSION OR w.LeaseExpirationTime IS NOT NULL)\n" +
                    $"AND (w.DequeueCount IS NULL OR w.DequeueCount < {SqlTriggerConstants.MaxDequeueCount}))) AS Changes;\n" +
                    $"IF @unprocessed_changes = 0 AND {newVersionNumber} > @current_version\n" +
                    $"BEGIN\n" +
                    $"UPDATE {_globalStateTable}\n" +
                    $"SET GlobalVersionNumber = {newVersionNumber}\n" +
                    $"WHERE UserTableID = {_userTableID};\n" +
                    $"DELETE FROM {_workerTable}\n" +
                    $"WHERE VersionNumber <= {newVersionNumber};\n" +
                    $"END\n";

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
                    _primaryKeys.Add(reader.GetString(0), type);
                }
                if (_primaryKeys.Count == 0)
                {
                    throw new InvalidOperationException($"Unable to determine the primary keys of user table {_userTable}. Potentially, the table does not have any primary " +
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

            /// <summary>
            /// Populates the _queryStrings dictionary with Lazy strings that will be initialized after _primaryKeys is populated
            /// </summary>
            private void LazyInitializeQueryStrings()
            {
                var primaryKeysSelectList = new Lazy<string>(() => string.Join(", ", _primaryKeys.Keys.Select(key => $"c.{key}")));
                var leftOuterJoinWorkerTable = new Lazy<string>(() => string.Join(" AND ", _primaryKeys.Keys.Select(key => $"c.{key} = w.{key}")));
                var leftOuterJoinUserTable = new Lazy<string>(() => string.Join(" AND ", _primaryKeys.Keys.Select(key => $"c.{key} = u.{key}")));
                var userTableColumnsSelectList = new Lazy<string>(() =>
                {
                    var nonPrimaryKeyCols = new List<string>();
                    foreach (var col in _userTableColumns)
                    {
                        if (!_primaryKeys.ContainsKey(col))
                        {
                            nonPrimaryKeyCols.Add(col);
                        }
                    }
                    if (nonPrimaryKeyCols.Count == 0)
                    {
                        return string.Empty;
                    }
                    else
                    {
                        return string.Join(", ", nonPrimaryKeyCols.Select(col => $"u.{col}")) + ", ";
                    }
                });

                _queryStrings.Add(SqlTriggerConstants.PrimaryKeysSelectList, primaryKeysSelectList);
                _queryStrings.Add(SqlTriggerConstants.UserTableColumnsSelectList, userTableColumnsSelectList);
                _queryStrings.Add(SqlTriggerConstants.LeftOuterJoinWorkerTable, leftOuterJoinWorkerTable);
                _queryStrings.Add(SqlTriggerConstants.LeftOuterJoinUserTable, leftOuterJoinUserTable);
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

            enum State
            {
                CheckingForChanges,
                ProcessingChanges
            }
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
            var getObjectIDQuery = $"SELECT OBJECT_ID(N\'{userTable}\', \'U\');";

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                // Don't think I need a transaction for this since I'm just reading data
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
    }
}