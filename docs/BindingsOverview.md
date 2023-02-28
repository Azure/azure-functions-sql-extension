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
    - [Change Tracking](#change-tracking)
    - [Functionality Overview](#functionality-overview)
    - [Internal State Tables](#internal-state-tables)
      - [az\_func.GlobalState](#az_funcglobalstate)
      - [az\_func.Leases\_\*](#az_funcleases_)
    - [Configuration for Trigger Bindings](#configuration-for-trigger-bindings)
      - [Sql\_Trigger\_MaxBatchSize](#sql_trigger_maxbatchsize)
      - [Sql\_Trigger\_PollingIntervalMs](#sql_trigger_pollingintervalms)
      - [Sql\_Trigger\_MaxChangesPerWorker](#sql_trigger_maxchangesperworker)
    - [Scaling for Trigger Bindings](#scaling-for-trigger-bindings)
    - [Retry support for Trigger Bindings](#retry-support-for-trigger-bindings)
      - [Startup retries](#startup-retries)
      - [Broken connection retries](#broken-connection-retries)
      - [Function exception retries](#function-exception-retries)

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

### Change Tracking

Azure SQL Trigger bindings utilize SQL [change tracking](https://docs.microsoft.com/sql/relational-databases/track-changes/about-change-tracking-sql-server) functionality to monitor the user table for changes. As such, it is necessary to enable change tracking on the SQL database and the SQL table before using the trigger support. The change tracking can be enabled through the following two queries.

1. Enabling change tracking on the SQL database:

    ```sql
    ALTER DATABASE ['your database name']
    SET CHANGE_TRACKING = ON
    (CHANGE_RETENTION = 2 DAYS, AUTO_CLEANUP = ON);
    ```

    The `CHANGE_RETENTION` option specifies the duration for which the changes are retained in the change tracking table. This may affect the trigger functionality. For example, if the user application is turned off for several days and then resumed, it will only be able to catch the changes that occurred in past two days with the above query. Hence, please update the value of `CHANGE_RETENTION` to suit your requirements. The `AUTO_CLEANUP` option is used to enable or disable the clean-up task that removes the stale data. Please refer to SQL Server documentation [here](https://docs.microsoft.com/sql/relational-databases/track-changes/enable-and-disable-change-tracking-sql-server#enable-change-tracking-for-a-database) for more information.

1. Enabling change tracking on the SQL table:

    ```sql
    ALTER TABLE dbo.Employees
    ENABLE CHANGE_TRACKING;
    ```

    For more information, please refer to the documentation [here](https://docs.microsoft.com/sql/relational-databases/track-changes/enable-and-disable-change-tracking-sql-server#enable-change-tracking-for-a-table). The trigger needs to have read access on the table being monitored for changes as well as to the change tracking system tables. It also needs write access to an `az_func` schema within the database, where it will create additional leases tables to store the trigger states and leases. Each function trigger will thus have an associated change tracking table and leases table.

    > **NOTE:** The leases table contains all columns corresponding to the primary key from the user table and three additional columns named `_az_func_ChangeVersion`, `_az_func_AttemptCount` and `_az_func_LeaseExpirationTime`. If any of the primary key columns happen to have the same name, that will result in an error message listing any conflicts. In this case, the listed primary key columns must be renamed for the trigger to work.

### Functionality Overview

The Azure SQL Trigger binding uses a polling loop to check for changes, triggering the user function when changes are detected. At a high level the loop looks like this :

```
while (true) {
    1. Get list of changes on table - up to a maximum number controlled by the Sql_Trigger_MaxBatchSize setting
    2. Trigger function with list of changes
    3. Wait for delay controlled by Sql_Trigger_PollingIntervalMs setting
}
```

Changes will always be processed in the order that their changes were made, with the oldest changes being processed first. A couple notes about this :

1. If changes to multiple rows are made at once the exact order that they'll be sent to the function is based on the order returned by the CHANGETABLE function
2. Changes are "batched" together for a row - if multiple changes are made to a row between each iteration of the loop than only a single change entry will exist for that row that shows the difference between the last processed state and the current state
3. If changes are made to a set of rows, and then another set of changes are made to half of those same rows then the half that wasn't changed a second time will be processed first. This is due to the above note with the changes being batched - the trigger will only see the "last" change made and use that for the order it processes them in

See [Work with change tracking](https://learn.microsoft.com/sql/relational-databases/track-changes/work-with-change-tracking-sql-server) for more information on change tracking and how it is used by applications such as Azure SQL triggers.

### Internal State Tables

The trigger functionality creates several tables to use for tracking the current state of the trigger. This allows state to be persisted across sessions and for multiple instances of a trigger binding to execute in parallel (for scaling purposes).

In addition, a schema named `az_func` will be created that the tables will belong to.

The login the trigger is configured to use must be given permissions to create these tables and schema. If not, then an error will be thrown and the trigger will fail to run.

If the tables are deleted or modified, then unexpected behavior may occur. To reset the state of the triggers, first stop all currently running functions with trigger bindings and then either truncate or delete the tables. The next time a function with a trigger binding is started, it will recreate the tables as necessary.

#### az_func.GlobalState

This table stores information about each function being executed, what table that function is watching and what the [last sync state](https://learn.microsoft.com/sql/relational-databases/track-changes/work-with-change-tracking-sql-server) that has been processed.

#### az_func.Leases_*

A `Leases_*` table is created for every unique instance of a function and table. The full name will be in the format `Leases_<FunctionId>_<TableId>` where `<FunctionId>` is generated from the function ID and `<TableId>` is the object ID of the table being tracked. Such as `Leases_7d12c06c6ddff24c_1845581613`.

This table is used to ensure that all changes are processed and that no change is processed more than once. This table consists of two groups of columns:

- A column for each column in the primary key of the target table - used to identify the row that it maps to in the target table
- A couple columns for tracking the state of each row. These are:
  - `_az_func_ChangeVersion` for the change version of the row currently being processed
  - `_az_func_AttemptCount` for tracking the number of times that a change has attempted to be processed to avoid getting stuck trying to process a change it's unable to handle
  - `_az_func_LeaseExpirationTime` for tracking when the lease on this row for a particular instance is set to expire. This ensures that if an instance exits unexpectedly another instance will be able to pick up and process any changes it had leases for after the expiration time has passed.

A row is created for every row in the target table that is modified. These are then cleaned up after the changes are processed for a set of changes corresponding to a change tracking sync version.

### Configuration for Trigger Bindings

This section goes over some of the configuration values you can use to customize SQL trigger bindings. See [How to Use Azure Function App Settings](https://learn.microsoft.com/azure/azure-functions/functions-how-to-use-azure-function-app-settings) to learn more.

#### Sql_Trigger_MaxBatchSize

This controls the maximum number of changes sent to the function during each iteration of the change processing loop.

#### Sql_Trigger_PollingIntervalMs

This controls the delay in milliseconds between processing each batch of changes.

#### Sql_Trigger_MaxChangesPerWorker

This controls the upper limit on the number of pending changes in the user table that are allowed per application-worker. If the count of changes exceeds this limit, it may result in a scale out. The setting only applies for Azure Function Apps with runtime driven scaling enabled. See the [Scaling](#scaling-for-trigger-bindings) section for more information.

### Scaling for Trigger Bindings

If your application containing functions with SQL trigger bindings is running as an Azure function app, it will be scaled automatically based on the amount of changes that are pending to be processed in the user table. As of today, we only support scaling of function apps running in Elastic Premium plan. To enable scaling, you will need to go the function app resource's page on Azure Portal, then to Configuration > 'Function runtime settings' and turn on 'Runtime Scale Monitoring'. For more information, check documentation on [Runtime Scaling](https://learn.microsoft.com/azure/azure-functions/event-driven-scaling#runtime-scaling). You can configure scaling parameters by going to 'Scale out (App Service plan)' setting on the function app's page. To understand various scale settings, please check the respective sections in [Azure Functions Premium plan](https://learn.microsoft.com/azure/azure-functions/functions-premium-plan?tabs=portal#eliminate-cold-starts)'s documentation.

There are a couple of checks made to decide on whether the host application needs to be scaled in or out. The rationale behind these checks is to ensure that the count of pending changes per application-worker stays below a certain maximum limit, which is defaulted to 1000, while also ensuring that the number of workers running stays minimal. The scaling decision is made based on the latest count of the pending changes and whether the last 5 times we checked the count, we found it to be continuously increasing or decreasing.

### Retry support for Trigger Bindings

#### Startup retries

If an exception occurs during startup then the host runtime will automatically attempt to restart the trigger listener with an exponential backoff strategy. These retries will continue until either the listener is successfully started or the startup is cancelled.

#### Broken connection retries

If the function successfully starts but then an error causes the connection to break (such as the server going offline) then the function will continue to try and reopen the connection until the function is either stopped or the connection succeeds. If the connection is successfully re-established then it will pick up processing changes where it left off.

Note that these retries are outside the built in idle connection retry logic that SqlClient has which can be configured with the [ConnectRetryCount](https://learn.microsoft.com/dotnet/api/system.data.sqlclient.sqlconnectionstringbuilder.connectretrycount) and [ConnectRetryInterval](https://learn.microsoft.com/dotnet/api/system.data.sqlclient.sqlconnectionstringbuilder.connectretryinterval) connection string options. The built-in idle connection retries will be attempted first and if those fail to reconnect then the trigger binding will attempt to re-establish the connection itself.

#### Function exception retries

If an exception occurs in the user function when processing changes then those rows will be retried again in 60 seconds. Other changes will be processed as normal during this time, but the rows that caused the exception will be ignored until the timeout period has elapsed.

If the function execution fails 5 times in a row for a given row then that row is completely ignored for all future changes.
