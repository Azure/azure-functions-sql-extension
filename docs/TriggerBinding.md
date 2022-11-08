# Trigger Binding

> **NOTE:** Trigger binding support is only available for C# functions at present.

## SqlTriggerAttribute
The trigger binding takes two [arguments](https://github.com/Azure/azure-functions-sql-extension/blob/main/src/TriggerBinding/SqlTriggerAttribute.cs)

- **TableName**: Passed as a constructor argument to the binding. Represents the name of the table to be monitored for changes.
- **ConnectionStringSetting**: Specifies the name of the app setting that contains the SQL connection string used to connect to a database. The connection string must follow the format specified [here](https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0).

The trigger binding can bind to type `IReadOnlyList<SqlChange<T>>`:

- **IReadOnlyList<SqlChange\<T\>>**: If there are multiple rows updated in the SQL table, the user function will get invoked with a batch of changes, where each element is a `SqlChange` object. Here `T` is a generic type-argument that can be substituted with a user-defined POCO, or Plain Old C# Object, representing the user table row. The POCO should therefore follow the schema of the queried table. See the [Query String](./QuickStart.md#query-string) section for an example of what the POCO should look like. The two properties of class `SqlChange<T>` are `Item` of type `T` which represents the table row and `Operation` of type `SqlChangeOperation` which indicates the kind of row operation (insert, update, or delete) that triggered the user function.

Note that for insert and update operations, the user function receives POCO object containing the latest values of table columns. For delete operation, only the properties corresponding to the primary keys of the row are populated.

Any time when the changes happen to the "Products" table, the user function will be invoked with a batch of changes. The changes are processed sequentially, so if there are a large number of changes pending to be processed, the function will be passed a batch containing the earliest changes first.

## Change Tracking

The trigger binding utilizes SQL [change tracking](https://docs.microsoft.com/sql/relational-databases/track-changes/about-change-tracking-sql-server) functionality to monitor the user table for changes. As such, it is necessary to enable change tracking on the SQL database and the SQL table before using the trigger support. The change tracking can be enabled through the following two queries.

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

## Internal State Tables

The trigger functionality creates several tables to use for tracking the current state of the trigger. This allows state to be persisted across sessions and for multiple instances of a trigger binding to execute in parallel (for scaling purposes).

In addition, a schema named `az_func` will be created that the tables will belong to.

The login the trigger is configured to use must be given permissions to create these tables and schema. If not, then an error will be thrown and the trigger will fail to run.

If the tables are deleted or modified, then unexpected behavior may occur. To reset the state of the triggers, first stop all currently running functions with trigger bindings and then either truncate or delete the tables. The next time a function with a trigger binding is started, it will recreate the tables as necessary.

### az_func.GlobalState

This table stores information about each function being executed, what table that function is watching and what the [last sync state](https://learn.microsoft.com/sql/relational-databases/track-changes/work-with-change-tracking-sql-server) that has been processed.

### az_func.Leases_*

A `Leases_*` table is created for every unique instance of a function and table. The full name will be in the format `Leases_<FunctionId>_<TableId>` where `<FunctionId>` is generated from the function ID and `<TableId>` is the object ID of the table being tracked. Such as `Leases_7d12c06c6ddff24c_1845581613`.

This table is used to ensure that all changes are processed and that no change is processed more than once. This table consists of two groups of columns:

   * A column for each column in the primary key of the target table - used to identify the row that it maps to in the target table
   * A couple columns for tracking the state of each row. These are:
     * `_az_func_ChangeVersion` for the change version of the row currently being processed
     * `_az_func_AttemptCount` for tracking the number of times that a change has attempted to be processed to avoid getting stuck trying to process a change it's unable to handle
     * `_az_func_LeaseExpirationTime` for tracking when the lease on this row for a particular instance is set to expire. This ensures that if an instance exits unexpectedly another instance will be able to pick up and process any changes it had leases for after the expiration time has passed.

A row is created for every row in the target table that is modified. These are then cleaned up after the changes are processed for a set of changes corresponding to a change tracking sync version.

## Setup

### .NET

```csharp
[FunctionName("ProductsTrigger")]
public static void Run(
    [SqlTrigger("Products", ConnectionStringSetting = "SqlConnectionString")]
    IReadOnlyList<SqlChange<Product>> changes,
    ILogger logger)
{
    foreach (SqlChange<Product> change in changes)
    {
        Product product = change.Item;
        logger.LogInformation($"Change operation: {change.Operation}");
        logger.LogInformation($"ProductID: {product.ProductID}, Name: {product.Name}, Cost: {product.Cost}");
    }
}
```

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./QuickStart.md#create-a-sql-server), and that you have the 'Employee.cs' file from the [Input Binding Guide](./InputBinding.md).

- Create a new file with the following content:

    ```csharp
    using System.Collections.Generic;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.WebJobs.Extensions.Sql;

    namespace Company.Function
    {
        public static class EmployeeTrigger
        {
            [FunctionName("EmployeeTrigger")]
            public static void Run(
                [SqlTrigger("[dbo].[Employees]", ConnectionStringSetting = "SqlConnectionString")]
                IReadOnlyList<SqlChange<Employee>> changes,
                ILogger logger)
            {
                foreach (SqlChange<Employee> change in changes)
                {
                    Employee employee = change.Item;
                    logger.LogInformation($"Change operation: {change.Operation}");
                    logger.LogInformation($"EmployeeID: {employee.EmployeeId}, FirstName: {employee.FirstName}, LastName: {employee.LastName}, Company: {employee.Company}, Team: {employee.Team}");
                }
            }
        }
    }
    ```

- *Skip these steps if you have not completed the output binding tutorial.*
    - Open your output binding file and modify some of the values. For example, change the value of Team column from 'Functions' to 'Azure SQL'.
    - Hit 'F5' to run your code. Click the link of the HTTP trigger from the output binding tutorial.
- Update, insert, or delete rows in your SQL table while the function app is running and observe the function logs.
- You should see the new log messages in the Visual Studio Code terminal containing the values of row-columns after the update operation.
- Congratulations! You have successfully created your first SQL trigger binding!

### Javascript

Javascript is not currently supported for SQL Trigger Bindings

### Python

Python is not currently supported for SQL Trigger Bindings

### Java

Java is not currently supported for SQL Trigger Bindings

## Configuration

This section goes over some of the configuration values you can use to customize the SQL bindings. See [How to Use Azure Function App Settings](https://learn.microsoft.com/azure/azure-functions/functions-how-to-use-azure-function-app-settings) to learn more.

### Sql_Trigger_BatchSize

This controls the number of changes processed at once before being sent to the triggered function.

### Sql_Trigger_PollingIntervalMs

This controls the delay in milliseconds between processing each batch of changes.

### Sql_Trigger_MaxChangesPerWorker

This controls the upper limit on the number of pending changes in the user table that are allowed per application-worker. If the count of changes exceeds this limit, it may result in a scale out. The setting only applies for Azure Function Apps with runtime driven scaling enabled. See the [Scaling](#scaling) section for more information.

## Scaling

If your application containing functions with SQL trigger bindings is running as an Azure function app, it will be scaled automatically based on the amount of changes that are pending to be processed in the user table. As of today, we only support scaling of function apps running in Elastic Premium plan. To enable scaling, you will need to go the function app resource's page on Azure Portal, then to Configuration > 'Function runtime settings' and turn on 'Runtime Scale Monitoring'. For more information, check documentation on [Runtime Scaling](https://learn.microsoft.com/azure/azure-functions/event-driven-scaling#runtime-scaling). You can configure scaling parameters by going to 'Scale out (App Service plan)' setting on the function app's page. To understand various scale settings, please check the respective sections in [Azure Functions Premium plan](https://learn.microsoft.com/azure/azure-functions/functions-premium-plan?tabs=portal#eliminate-cold-starts)'s documentation.

There are a couple of checks made to decide on whether the host application needs to be scaled in or out. The rationale behind these checks is to ensure that the count of pending changes per application-worker stays below a certain maximum limit, which is defaulted to 1000, while also ensuring that the number of workers running stays minimal. The scaling decision is made based on the latest count of the pending changes and whether the last 5 times we checked the count, we found it to be continuously increasing or decreasing.