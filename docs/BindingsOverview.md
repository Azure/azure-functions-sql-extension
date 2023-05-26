# Azure SQL bindings for Azure Functions - Overview

## Table of Contents

- [Azure SQL bindings for Azure Functions - Overview](#azure-sql-bindings-for-azure-functions---overview)
  - [Table of Contents](#table-of-contents)
  - [Input Binding](#input-binding)
    - [Retry support for Input Bindings](#retry-support-for-input-bindings)
  - [Output Binding](#output-binding)
    - [Primary Key Special Cases](#primary-key-special-cases)
      - [Identity Columns](#identity-columns)
      - [Columns with Default Values](#columns-with-default-values)
    - [Retry support for Output Bindings](#retry-support-for-output-bindings)
  - [Trigger Binding](#trigger-binding)
    - [Change Tracking Setup](#change-tracking-setup)
    - [Configuration for Trigger Bindings](#configuration-for-trigger-bindings)
      - [Sql\_Trigger\_MaxBatchSize](#sql_trigger_maxbatchsize)
      - [Sql\_Trigger\_PollingIntervalMs](#sql_trigger_pollingintervalms)
      - [Sql\_Trigger\_MaxChangesPerWorker](#sql_trigger_maxchangesperworker)
    - [Scaling for Trigger Bindings](#scaling-for-trigger-bindings)
    - [Retry support for Trigger Bindings](#retry-support-for-trigger-bindings)
      - [Startup retries](#startup-retries)
      - [Broken connection retries](#broken-connection-retries)
      - [Function exception retries](#function-exception-retries)
      - [Lease Tables clean up](#lease-tables-clean-up)

## Input Binding

Azure SQL Input bindings take a SQL query or stored procedure to run and returns the output to the function.

### Retry support for Input Bindings

There currently is no retry support for errors that occur for input bindings. If an exception occurs when an input binding is executed then the function code will not be executed. This may result in an error code being returned, for example an HTTP trigger will return a response with a status of 500 to indicate an error occurred.

## Output Binding

Azure SQL Output bindings take a list of rows and upserts them to the user table. Upserting means that if the primary key values of the row already exists in the table, the row is interpreted as an update, meaning that the values of the other columns in the table for that row are updated. If the primary key values do not exist in the table, the row values are inserted as new values. The upserting of the rows is batched by the output binding code.

  > **NOTE:** By default the Output binding uses the T-SQL [MERGE](https://docs.microsoft.com/sql/t-sql/statements/merge-transact-sql) statement which requires [SELECT](https://docs.microsoft.com/sql/t-sql/statements/merge-transact-sql#permissions) permissions on the target database.

### Primary Key Special Cases

Typically Output Bindings require two things :

1. The table being upserted to contains a Primary Key constraint (composed of one or more columns)
2. Each of those columns must be present in the POCO object used in the attribute

Normally either of these are false then an error will be thrown. Below are the situations in which this is not the case :

#### Identity Columns

In the case where one of the primary key columns is an identity column, there are two options based on how the function defines the output object:

1. If the identity column isn't included in the output object then a straight insert is always performed with the other column values. See [AddProductWithIdentityColumn](../samples/samples-csharp/OutputBindingSamples/AddProductWithIdentityColumn.cs) for an example.
2. If the identity column is included (even if it's an optional nullable value) then a merge is performed similar to what happens when no identity column is present. This merge will either insert a new row or update an existing row based on the existence of a row that matches the primary keys (including the identity column). See [AddProductWithIdentityColumnIncluded](../samples/samples-csharp/OutputBindingSamples/AddProductWithIdentityColumnIncluded.cs) for an example.

#### Columns with Default Values

In the case where one of the primary key columns has a default value, there are also two options based on how the function defines the output object:

1. If the column with a default value is not included in the output object, then a straight insert is always performed with the other values. See [AddProductWithDefaultPK](../samples/samples-csharp/OutputBindingSamples/AddProductWithDefaultPK.cs) for an example.
2. If the column with a default value is included then a merge is performed similar to what happens when no default column is present. If there is a nullable column with a default value, then the provided column value in the output object will be upserted even if it is null.

### Retry support for Output Bindings

There currently is no built-in support for errors that occur while executing output bindings. If an exception occurs when an output binding is executed then the function execution will stop. This may result in an error code being returned, for example an HTTP trigger will return a response with a status of 500 to indicate an error occurred.

If using a .NET Function then `IAsyncCollector` can be used, and the function code can handle exceptions thrown by the call to `FlushAsync()`.

See <https://github.com/Azure/Azure-Functions/issues/891> for further information.

## Trigger Binding

Azure SQL Trigger bindings monitor the user table for changes (i.e., row inserts, updates, and deletes) and invokes the function with updated rows.

For an in-depth explanation of how the trigger functions see the [Trigger Binding](./TriggerBinding.md) documentation.

### Change Tracking Setup

Azure SQL Trigger bindings utilize SQL [change tracking](https://docs.microsoft.com/sql/relational-databases/track-changes/about-change-tracking-sql-server) functionality to monitor the user table for changes. As such, it is necessary to enable change tracking on the SQL database and the SQL table before using the trigger support. The change tracking can be enabled through the following two queries.

1. Enabling change tracking on the SQL database:

    ```sql
    ALTER DATABASE ['your database name']
    SET CHANGE_TRACKING = ON
    (CHANGE_RETENTION = 2 DAYS, AUTO_CLEANUP = ON);
    ```

    The `CHANGE_RETENTION` option specifies the duration for which the changes are retained in the change tracking table. This may affect the trigger functionality. For example, if the user application is turned off for several days and then resumed, the database will contain the changes that occurred in past two days in the above setup example. Hence, please update the value of `CHANGE_RETENTION` to suit your requirements.

    The `AUTO_CLEANUP` option is used to enable or disable the clean-up task that removes the stale data. Please refer to SQL Server documentation [here](https://docs.microsoft.com/sql/relational-databases/track-changes/enable-and-disable-change-tracking-sql-server#enable-change-tracking-for-a-database) for more information.

1. Enabling change tracking on the SQL table:

    ```sql
    ALTER TABLE dbo.Employees
    ENABLE CHANGE_TRACKING;
    ```

    For more information, please refer to the documentation [here](https://docs.microsoft.com/sql/relational-databases/track-changes/enable-and-disable-change-tracking-sql-server#enable-change-tracking-for-a-table). The trigger needs to have read access on the table being monitored for changes as well as to the change tracking system tables. It also needs write access to an `az_func` schema within the database, where it will create additional leases tables to store the trigger states and leases. Each function trigger has an associated change tracking table and leases table.

    > **NOTE:** The leases table contains all columns corresponding to the primary key from the user table and three additional columns named `_az_func_ChangeVersion`, `_az_func_AttemptCount` and `_az_func_LeaseExpirationTime`. If any of the primary key columns happen to have the same name, that will result in an error message listing any conflicts. In this case, the listed primary key columns must be renamed for the trigger to work.

### Configuration for Trigger Bindings

This section goes over some of the configuration values you can use to customize SQL trigger bindings. See [How to Use Azure Function App Settings](https://learn.microsoft.com/azure/azure-functions/functions-how-to-use-azure-function-app-settings) to learn more.

#### Sql_Trigger_MaxBatchSize

The maximum number of changes sent to the function during each iteration of the change processing loop.

#### Sql_Trigger_PollingIntervalMs

The delay in milliseconds between processing each batch of changes.

#### Sql_Trigger_MaxChangesPerWorker

The upper limit on the number of pending changes in the user table that are allowed per application-worker. If the count of changes exceeds this limit, it may result in a scale out. The setting only applies for Azure Function Apps with runtime driven scaling enabled. See the [Scaling](#scaling-for-trigger-bindings) section for more information.

### Scaling for Trigger Bindings

If your application containing functions with SQL trigger bindings is running as an Azure function app, it will be scaled automatically based on the amount of changes that are pending to be processed in the user table. As of today, we only support scaling of function apps running in Elastic Premium plan with 'Runtime Scale Monitoring' enabled. To enable scaling, you will need to go the function app resource's page on Azure Portal, then to Configuration > 'Function runtime settings' and turn on 'Runtime Scale Monitoring'.

There are two types of scaling available:

- Incremental scaling - This scales the application serially, increasing or decreasing the workers by 1. There are a couple of checks made to decide on whether the host application needs to be scaled in or out. The rationale behind these checks is to ensure that the count of pending changes per application-worker stays below a certain maximum limit, controlled by [Sql_Trigger_MaxChangesPerWorker](#sql_trigger_maxchangesperworker), while also ensuring that the number of workers running stays minimal. The scaling decision is made based on the latest count of the pending changes and whether the last 5 samples we took were continually increasing or decreasing.

- Target Based Scaling - This type of scaling depends on the pending change count and the value of [dynamic concurrency](https://learn.microsoft.com/azure/azure-functions/functions-concurrency#dynamic-concurrency) which if not enabled is defaulted to [Sql_Trigger_MaxChangesPerWorker](#sql_trigger_maxchangesperworker). The target worker count is decided by dividing the pending changes by the concurrency value. The application scales out to the number of instances specified by the target worker count.

For more information, check documentation on [Runtime Scaling](https://learn.microsoft.com/azure/azure-functions/event-driven-scaling#runtime-scaling). You can configure scaling parameters by going to 'Scale out (App Service plan)' setting on the function app's page. To understand various scale settings, please check the respective sections in [Azure Functions Premium plan](https://learn.microsoft.com/azure/azure-functions/functions-premium-plan?tabs=portal#eliminate-cold-starts)'s documentation.

### Retry support for Trigger Bindings

#### Startup retries

If an exception occurs during startup then the host runtime will automatically attempt to restart the trigger listener with an exponential backoff strategy. These retries will continue until either the listener is successfully started or the startup is cancelled.

#### Broken connection retries

If the function successfully starts but then an error causes the connection to break (such as the server going offline) then the function will continue to try and reopen the connection until the function is either stopped or the connection succeeds. If the connection is successfully re-established then it will pick up processing changes where it left off.

Note that these retries are outside the built in idle connection retry logic that SqlClient has which can be configured with the [ConnectRetryCount](https://learn.microsoft.com/dotnet/api/system.data.sqlclient.sqlconnectionstringbuilder.connectretrycount) and [ConnectRetryInterval](https://learn.microsoft.com/dotnet/api/system.data.sqlclient.sqlconnectionstringbuilder.connectretryinterval) connection string options. The built-in idle connection retries will be attempted first and if those fail to reconnect then the trigger binding will attempt to re-establish the connection itself.

#### Function exception retries

If an exception occurs in the user function when processing changes then the batch of rows currently being processed will be retried again in 60 seconds. Other changes will be processed as normal during this time, but the rows in the batch that caused the exception will be ignored until the timeout period has elapsed.

If the function execution fails 5 times in a row for a given row then that row is completely ignored for all future changes. Because the rows in a batch are not deterministic, rows in a failed batch may end up in different batches in subsequent invocations. This means that not all rows in the failed batch will necessarily be ignored. If other rows in the batch were the ones causing the exception, the "good" rows may end up in a different batch that doesn't fail in future invocations.

You can run this query to see what rows have failed 5 times and are currently ignored, see [Leases table](./TriggerBinding.md#az_funcleases_) documentation for how to get the correct Leases table to query for your function.

```sql
SELECT * FROM [az_func].[Leases_<FunctionId>_<TableId>] WHERE _az_func_AttemptCount = 5
```

To reset a row and enable functions to try processing it again set the `_az_func_AttemptCount` value to 0.

e.g.

```sql
UPDATE [Products].[az_func].[Leases_<FunctionId>_<TableId>] SET _az_func_AttemptCount = 0 WHERE _az_func_AttemptCount = 5
```

> Note: This will reset ALL ignored rows. To reset only a specific row change the WHERE clause to select only the row you want to update.

e.g.

```sql
UPDATE [Products].[az_func].[Leases_<FunctionId>_<TableId>] SET _az_func_AttemptCount = 0 WHERE ProductId = 123
```

#### Lease Tables clean up

Before clean up, please see [Leases table](./TriggerBinding.md#az_funcleases_) documentation for understanding how they are created and used.

Why clean up?
1. You renamed your function/class/method name, which causes a new lease table to be created and the old one to be obsolete.
2. You created a trigger function that you no longer need and wish to clean up its associated data.
3. You want to reset your environment.
The Azure SQL Trigger does not currently handle automatically cleaning up any leftover objects, and so we have provided the below scripts to help guide you through doing that.

- Delete all the lease tables that haven't been accessed in `@CleanupAgeDays` days:

```sql
-- Deletes all the lease tables that haven't been accessed in @CleanupAgeDays days (set below)
-- and removes them from the GlobalState table.
USE <Insert DATABASE name here>;
DECLARE @TableName NVARCHAR(MAX);
DECLARE @UserFunctionId char(16);
DECLARE @UserTableId int;
DECLARE @CleanupAgeDays int = <Insert desired cleanup age in days here>;
DECLARE LeaseTable_Cursor CURSOR FOR

SELECT 'az_func.Leases_'+UserFunctionId+'_'+convert(varchar(100),UserTableID) as TABLE_NAME, UserFunctionID, UserTableID
FROM az_func.GlobalState
WHERE DATEDIFF(day, LastAccessTime, GETDATE()) > @CleanupAgeDays

OPEN LeaseTable_Cursor;

FETCH NEXT FROM LeaseTable_Cursor INTO @TableName, @UserFunctionId, @UserTableId;

WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT N'Dropping table ' + @TableName;
    EXEC ('DROP TABLE IF EXISTS ' + @TableName);
    PRINT N'Removing row from GlobalState for UserFunctionID = ' + @UserFunctionId + ' and UserTableID = ' + @UserTableId;
    DELETE FROM az_func.GlobalState WHERE UserFunctionID = @UserFunctionId and UserTableID = @UserTableId
    FETCH NEXT FROM LeaseTable_Cursor INTO @TableName, @UserFunctionId, @UserTableId;
END;

CLOSE LeaseTable_Cursor;

DEALLOCATE LeaseTable_Cursor;
```

- Clean up a specific lease table:

To find the name of the lease table associated with your function, look in the log output for a line such as this which is emitted when the trigger is started.

`SQL trigger Leases table: [az_func].[Leases_84d975fca0f7441a_901578250]`

This log message is at the `Information` level, so make sure your log level is set correctly.

```sql
-- Deletes the specified lease table and removes it from GlobalState table.
USE <Insert DATABASE name here>;
DECLARE @TableName NVARCHAR(MAX) = <Insert lease table name here>; -- e.g. '[az_func].[Leases_84d975fca0f7441a_901578250]
DECLARE @UserFunctionId char(16) = <Insert function ID here>; -- e.g. '84d975fca0f7441a' the first section of the lease table name [Leases_84d975fca0f7441a_901578250].
DECLARE @UserTableId int = <Insert table ID here>; -- e.g. '901578250' the second section of the lease table name [Leases_84d975fca0f7441a_901578250].
PRINT N'Dropping table ' + @TableName;
EXEC ('DROP TABLE IF EXISTS ' + @TableName);
PRINT N'Removing row from GlobalState for UserFunctionID = ' + @UserFunctionId + ' and UserTableID = ' + @UserTableId;
DELETE FROM az_func.GlobalState WHERE UserFunctionID = @UserFunctionId and UserTableID = @UserTableId
```

- Clear all trigger related data for a reset:

```sql
-- Deletes all the lease tables and clears them from the GlobalState table.
USE <Insert DATABASE name here>;
DECLARE @TableName NVARCHAR(MAX);
DECLARE @UserFunctionId char(16);
DECLARE @UserTableId int;
DECLARE LeaseTable_Cursor CURSOR FOR

SELECT 'az_func.Leases_'+UserFunctionId+'_'+convert(varchar(100),UserTableID) as TABLE_NAME, UserFunctionID, UserTableID
FROM az_func.GlobalState

OPEN LeaseTable_Cursor;

FETCH NEXT FROM LeaseTable_Cursor INTO @TableName, @UserFunctionId, @UserTableId;

WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT N'Dropping table ' + @TableName;
    EXEC ('DROP TABLE IF EXISTS ' + @TableName);
    PRINT N'Removing row from GlobalState for UserFunctionID = ' + @UserFunctionId + ' and UserTableID = ' + @UserTableId;
    DELETE FROM az_func.GlobalState WHERE UserFunctionID = @UserFunctionId and UserTableID = @UserTableId
    FETCH NEXT FROM LeaseTable_Cursor INTO @TableName, @UserFunctionId, @UserTableId;
END;

CLOSE LeaseTable_Cursor;

DEALLOCATE LeaseTable_Cursor;
```