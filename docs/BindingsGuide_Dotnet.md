# Azure SQL bindings for Azure Functions - .NET

## Input Binding

### SqlAttribute for Input Bindings

An input binding takes four [arguments](https://github.com/Azure/azure-functions-sql-extension/blob/main/src/SqlAttribute.cs):

- **CommandText**: Passed as a constructor argument to the binding. Represents either a query string or the name of a stored procedure.
- **CommandType**: Specifies whether CommandText is a query (`System.Data.CommandType.Text`) or a stored procedure (`System.Data.CommandType.StoredProcedure`)
- **Parameters**: The parameters to the query/stored procedure. This string must follow the format "@param1=param1,@param2=param2" where @param1 is the name of the parameter and param1 is the parameter value. Each pair of parameter name, parameter value is separated by a comma. Within each pair, the parameter name and value is separated by an equals sign. This means that neither the parameter name nor value can contain "," or "=". To specify a `NULL` parameter value, do "@param1=null,@param2=param2". To specify an empty string as a value, do "@param1=,@param2=param2", i.e. do not put any text after the equals sign of the corresponding parameter name. This argument is auto-resolvable (see Query String examples).
- **ConnectionStringSetting**: Specifies the name of the app setting that contains the SQL connection string used to connect to a database. The connection string must follow the format specified [here](https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0).

The following are valid binding types for the result of the query/stored procedure execution:

- **IEnumerable&lt;T&gt;**: Each element is a row of the result represented by `T`, where `T` is a user-defined POCO, or Plain Old C# Object. `T` should follow the structure of a row in the queried table. See the [Query String](#query-string) section for an example of what `T` should look like.
- **IAsyncEnumerable&lt;T&gt;**: Each element is again a row of the result represented by `T`, but the rows are retrieved "lazily". A row of the result is only retrieved when `MoveNextAsync` is called on the enumerator. This is useful in the case that the query can return a very large amount of rows.
- **String**: A JSON string representation of the rows of the result (an example is provided [here](https://github.com/Azure/azure-functions-sql-extension/blob/main/samples/samples-csharp/InputBindingSamples/GetProductsString.cs)).
- **SqlCommand**: The SqlCommand is populated with the appropriate query and parameters, but the associated connection is not opened. It is the responsiblity of the user to execute the command and read in the results. This is useful in the case that the user wants more control over how the results are read in. An example is provided [here](https://github.com/Azure/azure-functions-sql-extension/blob/main/samples/samples-csharp/InputBindingSamples/GetProductsSqlCommand.cs).

The repo contains examples of each of these binding types [here](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-csharp/InputBindingSamples). A few examples are also included [below](#samples).

### Setup for Input Bindings

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./QuickStart.md#create-a-sql-server).

- Open your app that you created in [Create a Function App](./QuickStart.md#create-a-function-app) in VS Code
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger -> (Provide a function name) -> Company.namespace -> anonymous
- In the file that opens, replace the `public static async Task<IActionResult> Run` block with the below code.

    ```csharp
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "employees")] HttpRequest req,
        ILogger log,
        [Sql("select * from Employees",
        CommandType = System.Data.CommandType.Text,
        ConnectionStringSetting = "SqlConnectionString")]
        IEnumerable<Employee> employee)
    {
        return new OkObjectResult(employee);
    }
    ```

    *In the above, "select * from Employees" is the SQL script run by the input binding. The CommandType on the line below specifies whether the first line is a query or a stored procedure. On the next line, the ConnectionStringSetting specifies that the app setting that contains the SQL connection string used to connect to the database is "SqlConnectionString." For more information on this, see the [SqlAttribute for Input Bindings](#sqlattribute-for-input-bindings) section*

- Add 'using System.Collections.Generic;' to the namespaces list at the top of the page.
- Currently, there is an error for the IEnumerable. We'll fix this by creating an Employee class.
- Create a new file and call it 'Employee.cs'
- Paste the below in the file. These are the column names of our SQL table.

    ```csharp
    namespace Company.Function {
        public class Employee{
            public int EmployeeId { get; set; }
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string Company { get; set; }
            public string Team { get; set; }
        }
    }
    ```

- Navigate back to your HttpTrigger file. We can ignore the 'Run' warning for now.
- Open the local.settings.json file, and in the brackets for "Values," verify there is a 'SqlConnectionString.' If not, add it.
- Hit 'F5' to run your code. This will start up the Functions Host with a local HTTP Trigger and SQL Input Binding.
- Click the link that appears in your terminal.
- You should see your database output in the browser window.
- Congratulations! You have successfully created your first SQL input binding!

### Samples for Input Bindings

#### Query String

The input binding executes the "select * from Products where Cost = @Cost" query, returning the result as an `IEnumerable<Product>`, where Product is a user-defined POCO. The *Parameters* argument passes the `{cost}` specified in the URL that triggers the function, `getproducts/{cost}`, as the value of the `@Cost` parameter in the query. *CommandType* is set to `System.Data.CommandType.Text`, since the constructor argument of the binding is a raw query.

```csharp
[FunctionName("GetProducts")]
  public static IActionResult Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts/{cost}")]
      HttpRequest req,
      [Sql("select * from Products where Cost = @Cost",
          CommandType = System.Data.CommandType.Text,
          Parameters = "@Cost={cost}",
          ConnectionStringSetting = "SqlConnectionString")]
      IEnumerable<Product> products)
  {
      return (ActionResult)new OkObjectResult(products);
  }
```

`Product` is a user-defined POCO that follows the structure of the Products table. It represents a row of the Products table, with field names and types copying those of the Products table schema. For example, if the Products table has three columns of the form

- **ProductID**: int
- **Name**: varchar
- **Cost**: int

Then the `Product` class would look like

```csharp
public class Product
{
    public int ProductID { get; set; }

    public string Name { get; set; }

    public int Cost { get; set; }

}
```

#### Empty Parameter Value

In this case, the parameter value of the `@Name` parameter is an empty string.

```csharp
[FunctionName("GetProductsNameEmpty")]
  public static IActionResult Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-nameempty/{cost}")]
      HttpRequest req,
      [Sql("select * from Products where Cost = @Cost and Name = @Name",
          CommandType = System.Data.CommandType.Text,
          Parameters = "@Cost={cost},@Name=",
          ConnectionStringSetting = "SqlConnectionString")]
      IEnumerable<Product> products)
  {
      return (ActionResult)new OkObjectResult(products);
  }
  ```

#### Null Parameter Value

If the `{name}` specified in the `getproducts-namenull/{name}` URL is "null", the query returns all rows for which the Name column is `NULL`. Otherwise, it returns all rows for which the value of the Name column matches the string passed in `{name}`

```csharp
[FunctionName("GetProductsNameNull")]
  public static IActionResult Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-namenull/{name}")]
      HttpRequest req,
      [Sql("if @Name is null select * from Products where Name is null else select * from Products where @Name = name",
          CommandType = System.Data.CommandType.Text,
          Parameters = "@Name={name}",
          ConnectionStringSetting = "SqlConnectionString")]
      IEnumerable<Product> products)
  {
      return (ActionResult)new OkObjectResult(products);
  }
```

#### Stored Procedure

`SelectsProductCost` is the name of a procedure stored in the user's database. In this case, *CommandType* is `System.Data.CommandType.StoredProcedure`. The parameter value of the `@Cost` parameter in the procedure is once again the `{cost}` specified in the `getproducts-storedprocedure/{cost}` URL.

```csharp
[FunctionName("GetProductsStoredProcedure")]
  public static IActionResult Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-storedprocedure/{cost}")]
      HttpRequest req,
      [Sql("SelectProductsCost",
          CommandType = System.Data.CommandType.StoredProcedure,
          Parameters = "@Cost={cost}",
          ConnectionStringSetting = "SqlConnectionString")]
      IEnumerable<Product> products)
  {
      return (ActionResult)new OkObjectResult(products);
  }
```

#### IAsyncEnumerable

Using the `IAsyncEnumerable` binding generally requires that the `Run` function be `async`. It is also important to call `DisposeAsync` at the end of function execution to make sure all resources used by the enumerator are freed.

```csharp
public static async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-async/{cost}")]
     HttpRequest req,
    [Sql("select * from Products where cost = @Cost",
         CommandType = System.Data.CommandType.Text,
         Parameters = "@Cost={cost}",
         ConnectionStringSetting = "SqlConnectionString")]
     IAsyncEnumerable<Product> products)
{
    var enumerator = products.GetAsyncEnumerator();
    var productList = new List<Product>();
    while (await enumerator.MoveNextAsync())
    {
        productList.Add(enumerator.Current);
    }
    await enumerator.DisposeAsync();
    return (ActionResult)new OkObjectResult(productList);
}
```

## Output Binding

### SqlAttribute for Output Bindings

The output binding takes a list of rows to be upserted into a user table. If the primary key value of the row already exists in the table, the row is interpreted as an update, meaning that the values of the other columns in the table for that primary key are updated. If the primary key value does not exist in the table, the row is interpreted as an insert. The upserting of the rows is batched by the output binding code.

  > **NOTE:** By default the Output binding uses the T-SQL [MERGE](https://docs.microsoft.com/sql/t-sql/statements/merge-transact-sql) statement which requires [SELECT](https://docs.microsoft.com/sql/t-sql/statements/merge-transact-sql#permissions) permissions on the target database.

The output binding takes two [arguments](https://github.com/Azure/azure-functions-sql-extension/blob/main/src/SqlAttribute.cs):

- **CommandText**: Passed as a constructor argument to the binding. Represents the name of the table into which rows will be upserted.
- **ConnectionStringSetting**: Specifies the name of the app setting that contains the SQL connection string used to connect to a database. The connection string must follow the format specified [here](https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0).

The following are valid binding types for the rows to be upserted into the table:

- **ICollector&lt;T&gt;/IAsyncCollector&lt;T&gt;**: Each element is a row represented by `T`, where `T` is a user-defined POCO, or Plain Old C# Object. `T` should follow the structure of a row in the queried table. See the [Query String](./InputBinding.md#query-string) for an example of what `T` should look like.
- **T**: Used when just one row is to be upserted into the table.
- **T[]**: Each element is again a row of the result represented by `T`. This output binding type requires manual instantiation of the array in the function.

The repo contains examples of each of these binding types [here](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-csharp/OutputBindingSamples). A few examples are also included [below](#samples).

### Setup for Output Bindings

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./QuickStart.md#create-a-sql-server), and that you have the 'Employee.cs' class from the [Input Binding Guide](./InputBinding.md#net).

- Open your app in VS Code
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger ->  (Provide a function name) -> Company.namespace is fine -> anonymous
- In the file that opens, replace the `public static async Task<IActionResult> Run` block with the below code

    ```csharp
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addemployees-array")]
        HttpRequest req, ILogger log,
        [Sql("dbo.Employees",
        ConnectionStringSetting = "SqlConnectionString")]
        out Employee[] output)
    {
        output = new Employee[]
            {
                new Employee
                {
                    EmployeeId = 1,
                    FirstName = "Hello",
                    LastName = "World",
                    Company = "Microsoft",
                    Team = "Functions"
                },
                new Employee
                {
                    EmployeeId = 2,
                    FirstName = "Hi",
                    LastName = "SQLupdate",
                    Company = "Microsoft",
                    Team = "Functions"
                },
            };

        return new CreatedResult($"/api/addemployees-array", output);
    }
    ```

    *In the above, "dbo.Employees" is the name of the table our output binding is upserting into. The line below is similar to the input binding and specifies where our SqlConnectionString is. For more information on this, see the [SqlAttribute for Output Bindings](#sqlattribute-for-output-bindings) section*

- Hit 'F5' to run your code. Click the link to upsert the output array values in your SQL table. Your upserted values should launch in the browser.
- Congratulations! You have successfully created your first SQL output binding!

### Samples for Output Bindings

#### ICollector&lt;T&gt;/IAsyncCollector&lt;T&gt;

When using an `ICollector`, it is not necessary to instantiate it. The function can add rows to the `ICollector` directly, and its contents are automatically upserted once the function exits.

 ```csharp
[FunctionName("AddProductsCollector")]
public static IActionResult Run(
[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproducts-collector")] HttpRequest req,
[Sql("Products", ConnectionStringSetting = "SqlConnectionString")] ICollector<Product> products)
{
    var newProducts = GetNewProducts(5000);
    foreach (var product in newProducts)
    {
        products.Add(product);
    }
    return new CreatedResult($"/api/addproducts-collector", "done");
}
```

It is also possible to force an upsert within the function by calling `FlushAsync()` on an `IAsyncCollector`

```csharp
[FunctionName("AddProductsAsyncCollector")]
public static async Task<IActionResult> Run(
[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproducts-asynccollector")] HttpRequest req,
[Sql("Products", ConnectionStringSetting = "SqlConnectionString")] IAsyncCollector<Product> products)
{
    var newProducts = GetNewProducts(5000);
    foreach (var product in newProducts)
    {
        await products.AddAsync(product);
    }
    // Rows are upserted here
    await products.FlushAsync();

    newProducts = GetNewProducts(5000);
    foreach (var product in newProducts)
    {
        await products.AddAsync(product);
    }
    return new CreatedResult($"/api/addproducts-collector", "done");
}
```

#### Array

This output binding type requires explicit instantiation within the function body. Note also that the `Product[]` array must be prefixed by `out` when attached to the output binding

``` csharp
[FunctionName("AddProductsArray")]
public static IActionResult Run(
[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproducts-array")]
    HttpRequest req,
[Sql("dbo.Products", ConnectionStringSetting = "SqlConnectionString")] out Product[] output)
{
    // Suppose that the ProductID column is the primary key in the Products table, and the
    // table already contains a row with ProductID = 1. In that case, the row will be updated
    // instead of inserted to have values Name = "Cup" and Cost = 2.
    output = new Product[2];
    var product = new Product();
    product.ProductID = 1;
    product.Name = "Cup";
    product.Cost = 2;
    output[0] = product;
    product = new Product();
    product.ProductID = 2;
    product.Name = "Glasses";
    product.Cost = 12;
    output[1] = product;
    return new CreatedResult($"/api/addproducts-array", output);
}
```

#### Single Row

When binding to a single row, it is also necessary to prefix the row with `out`

```csharp
[FunctionName("AddProduct")]
public static IActionResult Run(
[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct")]
    HttpRequest req,
[Sql("Products", ConnectionStringSetting = "SqlConnectionString")] out Product product)
{
    product = new Product
    {
        Name = req.Query["name"],
        ProductID = int.Parse(req.Query["id"]),
        Cost = int.Parse(req.Query["cost"])
    };
    return new CreatedResult($"/api/addproduct", product);
}
```

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

## Trigger Binding

### SqlTriggerAttribute

The trigger binding takes two [arguments](https://github.com/Azure/azure-functions-sql-extension/blob/main/src/TriggerBinding/SqlTriggerAttribute.cs)

- **TableName**: Passed as a constructor argument to the binding. Represents the name of the table to be monitored for changes.
- **ConnectionStringSetting**: Specifies the name of the app setting that contains the SQL connection string used to connect to a database. The connection string must follow the format specified [here](https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0).

The trigger binding can bind to type `IReadOnlyList<SqlChange<T>>`:

- **IReadOnlyList<SqlChange\<T\>>**: If there are multiple rows updated in the SQL table, the user function will get invoked with a batch of changes, where each element is a `SqlChange` object. Here `T` is a generic type-argument that can be substituted with a user-defined POCO, or Plain Old C# Object, representing the user table row. The POCO should therefore follow the schema of the queried table. See the [Query String](./QuickStart.md#query-string) section for an example of what the POCO should look like. The two properties of class `SqlChange<T>` are `Item` of type `T` which represents the table row and `Operation` of type `SqlChangeOperation` which indicates the kind of row operation (insert, update, or delete) that triggered the user function.

Note that for insert and update operations, the user function receives POCO object containing the latest values of table columns. For delete operation, only the properties corresponding to the primary keys of the row are populated.

Any time when the changes happen to the "Products" table, the user function will be invoked with a batch of changes. The changes are processed sequentially, so if there are a large number of changes pending to be processed, the function will be passed a batch containing the earliest changes first.

### Change Tracking

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

   * A column for each column in the primary key of the target table - used to identify the row that it maps to in the target table
   * A couple columns for tracking the state of each row. These are:
     * `_az_func_ChangeVersion` for the change version of the row currently being processed
     * `_az_func_AttemptCount` for tracking the number of times that a change has attempted to be processed to avoid getting stuck trying to process a change it's unable to handle
     * `_az_func_LeaseExpirationTime` for tracking when the lease on this row for a particular instance is set to expire. This ensures that if an instance exits unexpectedly another instance will be able to pick up and process any changes it had leases for after the expiration time has passed.

A row is created for every row in the target table that is modified. These are then cleaned up after the changes are processed for a set of changes corresponding to a change tracking sync version.

### Setup for Trigger Bindings

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

### Configuration for Trigger Bindings

This section goes over some of the configuration values you can use to customize the SQL bindings. See [How to Use Azure Function App Settings](https://learn.microsoft.com/azure/azure-functions/functions-how-to-use-azure-function-app-settings) to learn more.

#### Sql_Trigger_BatchSize

This controls the number of changes processed at once before being sent to the triggered function.

#### Sql_Trigger_PollingIntervalMs

This controls the delay in milliseconds between processing each batch of changes.

#### Sql_Trigger_MaxChangesPerWorker

This controls the upper limit on the number of pending changes in the user table that are allowed per application-worker. If the count of changes exceeds this limit, it may result in a scale out. The setting only applies for Azure Function Apps with runtime driven scaling enabled. See the [Scaling](#scaling) section for more information.

### Scaling for Trigger Bindings

If your application containing functions with SQL trigger bindings is running as an Azure function app, it will be scaled automatically based on the amount of changes that are pending to be processed in the user table. As of today, we only support scaling of function apps running in Elastic Premium plan. To enable scaling, you will need to go the function app resource's page on Azure Portal, then to Configuration > 'Function runtime settings' and turn on 'Runtime Scale Monitoring'. For more information, check documentation on [Runtime Scaling](https://learn.microsoft.com/azure/azure-functions/event-driven-scaling#runtime-scaling). You can configure scaling parameters by going to 'Scale out (App Service plan)' setting on the function app's page. To understand various scale settings, please check the respective sections in [Azure Functions Premium plan](https://learn.microsoft.com/azure/azure-functions/functions-premium-plan?tabs=portal#eliminate-cold-starts)'s documentation.

There are a couple of checks made to decide on whether the host application needs to be scaled in or out. The rationale behind these checks is to ensure that the count of pending changes per application-worker stays below a certain maximum limit, which is defaulted to 1000, while also ensuring that the number of workers running stays minimal. The scaling decision is made based on the latest count of the pending changes and whether the last 5 times we checked the count, we found it to be continuously increasing or decreasing.
