// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal static class SqlTriggerConstants
    {
        public const string SchemaName = "az_func";

        public const string GlobalStateTableName = "[" + SchemaName + "].[GlobalState]";

        public const string LeasesTableNameFormat = "[" + SchemaName + "].[Leases_{0}]";

        public const string UserDefinedLeasesTableNameFormat = "[" + SchemaName + "].{0}";

        public const string LeasesTableChangeVersionColumnName = "_az_func_ChangeVersion";
        public const string LeasesTableAttemptCountColumnName = "_az_func_AttemptCount";
        public const string LeasesTableLeaseExpirationTimeColumnName = "_az_func_LeaseExpirationTime";
        public const string SysChangeVersionColumnName = "SYS_CHANGE_VERSION";
        public const string LastAccessTimeColumnName = "LastAccessTime";
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
        /// The resource name to use for getting the global application lock. This is used for DDL operations
        /// during startup that touch shared objects (schema creation, GlobalState table creation).
        /// </summary>
        public const string GlobalAppLockResource = "_az_func_Trigger";
        /// <summary>
        /// The prefix for per-table scoped application locks. The full resource name is formed by appending
        /// the user table ID, e.g. "_az_func_TT_12345". This allows functions monitoring different tables
        /// to execute their transactions in parallel without blocking each other, while still preventing
        /// deadlocks between functions (or scaled-out instances) that monitor the same table.
        /// </summary>
        public const string TableAppLockResourcePrefix = "_az_func_TT_";
        /// <summary>
        /// Timeout for acquiring the application lock - 30sec chosen as a reasonable value to ensure we aren't
        /// hanging infinitely while also giving plenty of time for the blocking transaction to complete.
        /// </summary>
        public const int AppLockTimeoutMs = 30000;

        /// <summary>
        /// T-SQL statements for getting the global application lock. This is used only for startup DDL operations
        /// that create shared objects (the az_func schema and the GlobalState table).
        ///
        /// See the following articles for more information on locking in MSSQL
        /// https://learn.microsoft.com/sql/relational-databases/sql-server-transaction-locking-and-row-versioning-guide
        /// https://learn.microsoft.com/sql/t-sql/statements/set-transaction-isolation-level-transact-sql
        /// https://learn.microsoft.com/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql
        /// </summary>
        public static readonly string GlobalAppLockStatements = $@"DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = '{GlobalAppLockResource}',
                            @LockMode = 'Exclusive',
                            @LockTimeout = {AppLockTimeoutMs}
                IF @result < 0
                BEGIN
                    RAISERROR('Unable to acquire exclusive lock on {GlobalAppLockResource}. Result = %d', 16, 1, @result)
                END;";

        /// <summary>
        /// Generates T-SQL statements for getting a per-table scoped application lock. This is used for all
        /// runtime operations that only touch data specific to a single table (GlobalState row, leases table,
        /// change tracking queries). By scoping the lock to the table level, functions monitoring different
        /// tables can process changes in parallel without blocking each other.
        ///
        /// The lock is still needed at the table level to prevent deadlocks when multiple scaled-out instances
        /// or multiple functions monitor the same table, since they share GlobalState rows and may access
        /// overlapping change tracking data.
        /// </summary>
        /// <param name="userTableId">The SQL Server object ID of the user table</param>
        /// <returns>T-SQL statements that acquire an exclusive app lock scoped to the given table</returns>
        public static string GetTableScopedAppLockStatements(int userTableId)
        {
            string resource = $"{TableAppLockResourcePrefix}{userTableId}";
            return $@"DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = '{resource}',
                            @LockMode = 'Exclusive',
                            @LockTimeout = {AppLockTimeoutMs}
                IF @result < 0
                BEGIN
                    RAISERROR('Unable to acquire exclusive lock on {resource}. Result = %d', 16, 1, @result)
                END;";
        }

        /// <summary>
        /// There is already an object named '%.*ls' in the database.
        /// </summary>
        public const int ObjectAlreadyExistsErrorNumber = 2714;
    }
}
