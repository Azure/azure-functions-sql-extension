// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal static class SqlTriggerConstants
    {
        public const string SchemaName = "az_func";

        public const string GlobalStateTableName = "[" + SchemaName + "].[GlobalState]";

        public const string LeasesTableNameFormat = "[" + SchemaName + "].[Leases_{0}]";

        public const string LeasesTableChangeVersionColumnName = "_az_func_ChangeVersion";
        public const string LeasesTableAttemptCountColumnName = "_az_func_AttemptCount";
        public const string LeasesTableLeaseExpirationTimeColumnName = "_az_func_LeaseExpirationTime";
        public const string SysChangeVersionColumnName = "SYS_CHANGE_VERSION";
        /// <summary>
        /// The column names that are used in internal state tables and so can't exist in the target table
        /// since that shares column names with the primary keys from each user table being monitored.
        /// </summary>
        public static readonly string[] ReservedColumnNames = new string[]
        {
                    LeasesTableChangeVersionColumnName,
                    LeasesTableAttemptCountColumnName,
                    LeasesTableLeaseExpirationTimeColumnName
        };

        /// <summary>
        /// Deprecated config value for MaxBatchSize, kept for backwards compat reasons
        /// </summary>
        public const string ConfigKey_SqlTrigger_BatchSize = "Sql_Trigger_BatchSize";
        public const string ConfigKey_SqlTrigger_MaxBatchSize = "Sql_Trigger_MaxBatchSize";
        public const string ConfigKey_SqlTrigger_PollingInterval = "Sql_Trigger_PollingIntervalMs";
        public const string ConfigKey_SqlTrigger_MaxChangesPerWorker = "Sql_Trigger_MaxChangesPerWorker";

        /// <summary>
        /// The resource name to use for getting the application lock. We use the same resource name for all instances
        /// of the function because there is some shared state across all the functions.
        /// </summary>
        /// <remarks>A future improvement could be to make unique application locks for each FuncId/TableId combination so that functions
        /// working on different tables aren't blocking each other</remarks>
        public const string AppLockResource = "_az_func_Trigger";
        /// <summary>
        /// Timeout for acquiring the application lock - 30sec chosen as a reasonable value to ensure we aren't
        /// hanging infinitely while also giving plenty of time for the blocking transaction to complete.
        /// </summary>
        public const int AppLockTimeoutMs = 30000;

        /// <summary>
        /// T-SQL statements for getting an application lock. This is used to prevent deadlocks - primarily when multiple instances
        /// of a function are running in parallel.
        ///
        /// The trigger heavily uses transactions to ensure atomic changes, that way if an error occurs during any step of a process we aren't left
        /// with an incomplete state. Because of this, locks are placed on rows that are read/modified during the transaction, but the lock isn't
        /// applied until the statement itself is executed. Some transactions have many statements executed in a row that touch a number of different
        /// tables so it's very easy for two transactions to get in a deadlock depending on the speed they execute their statements and the order they
        /// are processed in.
        ///
        /// So to avoid this we use application locks to ensure that anytime we enter a transaction we first guarantee that we're the only transaction
        /// currently making any changes to the tables, which means that we're guaranteed not to have any deadlocks - albeit at the cost of speed. This
        /// is acceptable for now, although further investigation could be done into using multiple resources to lock on (such as a different one for each
        /// table) to increase the parallelization of the transactions.
        ///
        /// See the following articles for more information on locking in MSSQL
        /// https://learn.microsoft.com/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide
        /// https://learn.microsoft.com/sql/t-sql/statements/set-transaction-isolation-level-transact-sql
        /// https://learn.microsoft.com/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql
        /// </summary>
        public static readonly string AppLockStatements = $@"DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = '{AppLockResource}',
                            @LockMode = 'Exclusive',
                            @LockTimeout = {AppLockTimeoutMs}
                IF @result < 0
                BEGIN
                    RAISERROR('Unable to acquire exclusive lock on {AppLockResource}. Result = %d', 16, 1, @result)
                END;";

        /// <summary>
        /// There is already an object named '%.*ls' in the database.
        /// </summary>
        public const int ObjectAlreadyExistsErrorNumber = 2714;
    }
}
