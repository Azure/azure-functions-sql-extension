# Azure SQL bindings for Azure Functions - .NET

## Table of Contents

- [Azure SQL bindings for Azure Functions - .NET](#azure-sql-bindings-for-azure-functions---net)
  - [Table of Contents](#table-of-contents)
  - [Setup Function Project](#setup-function-project)
  - [Input Binding](#input-binding)
    - [SqlAttribute for Input Bindings](#sqlattribute-for-input-bindings)
    - [Setup for Input Bindings](#setup-for-input-bindings)
    - [Samples for Input Bindings](#samples-for-input-bindings)
      - [Query String](#query-string)
      - [Empty Parameter Value](#empty-parameter-value)
      - [Null Parameter Value](#null-parameter-value)
      - [Stored Procedure](#stored-procedure)
      - [IAsyncEnumerable](#iasyncenumerable)
  - [Output Binding](#output-binding)
    - [SqlAttribute for Output Bindings](#sqlattribute-for-output-bindings)
    - [Setup for Output Bindings](#setup-for-output-bindings)
    - [Samples for Output Bindings](#samples-for-output-bindings)
      - [ICollector\<T\>/IAsyncCollector\<T\>](#icollectortiasynccollectort)
      - [Array](#array)
      - [Single Row](#single-row)
  - [Trigger Binding](#trigger-binding)
    - [SqlTriggerAttribute](#sqltriggerattribute)
    - [Setup for Trigger Bindings](#setup-for-trigger-bindings)

## Setup Function Project

These instructions will guide you through creating your Function Project and adding the SQL binding extension. This only needs to be done once for every function project you create. If you have one created already you can skip this step.

1. Install [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local)

2. Create a function project for .NET:

    ```bash
    mkdir MyApp
    cd MyApp
    func init --worker-runtime dotnet
    ```

3. Enable SQL bindings on the function project. More information can be found in the [Azure SQL bindings for Azure Functions docs](https://aka.ms/sqlbindings).

    Install the extension.

    ```powershell
    dotnet add package Microsoft.Azure.WebJobs.Extensions.Sql --prerelease
    ```

## Input Binding

See [Input Binding Overview](./BindingsOverview.md#input-binding) for general information about the Azure SQL Input binding.

### SqlAttribute for Input Bindings

The [SqlAttribute](https://github.com/Azure/azure-functions-sql-extension/blob/main/src/SqlAttribute.cs) for Input bindings takes four arguments:

- **CommandText**: Represents either a query string or the name of a stored procedure based on the value of the CommandType.
- **ConnectionStringSetting**: Specifies the name of the app setting that contains the SQL connection string used to connect to a database. The connection string must follow the format specified [here](https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0).
- **CommandType**: Specifies whether CommandText is a query (`System.Data.CommandType.Text`) or a stored procedure (`System.Data.CommandType.StoredProcedure`). Defaults to `CommandType.Text`.
- **Parameters**: The parameters to the query/stored procedure. This string must follow the format "@param1=param1,@param2=param2" where @param1 is the name of the parameter and param1 is the parameter value. Each pair of parameter name, parameter value is separated by a comma. Within each pair, the parameter name and value is separated by an equals sign. This means that neither the parameter name nor value can contain "," or "=". To specify a `NULL` parameter value, do "@param1=null,@param2=param2". To specify an empty string as a value, do "@param1=,@param2=param2", i.e. do not put any text after the equals sign of the corresponding parameter name. This argument is auto-resolvable (see Query String examples).

The following are valid binding types for the result of the query/stored procedure execution:

- **IEnumerable&lt;T&gt;**: Each element is a row of the result represented by `T`, where `T` is a user-defined POCO, or Plain Old C# Object. `T` should follow the structure of a row in the queried table. See the [Query String](#query-string) section for an example of what `T` should look like.
- **IAsyncEnumerable&lt;T&gt;**: Each element is again a row of the result represented by `T`, but the rows are retrieved "lazily". A row of the result is only retrieved when `MoveNextAsync` is called on the enumerator. This is useful in the case that the query can return a very large amount of rows.
- **String**: A JSON string representation of the rows of the result (an example is provided [here](https://github.com/Azure/azure-functions-sql-extension/blob/main/samples/samples-csharp/InputBindingSamples/GetProductsString.cs)).
- **SqlCommand**: The SqlCommand is populated with the appropriate query and parameters, but the associated connection is not opened. It is the responsiblity of the user to execute the command and read in the results. This is useful in the case that the user wants more control over how the results are read in. An example is provided [here](https://github.com/Azure/azure-functions-sql-extension/blob/main/samples/samples-csharp/InputBindingSamples/GetProductsSqlCommand.cs).

The repo contains examples of each of these binding types [here](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-csharp/InputBindingSamples). A few examples are also included [below](#samples-for-input-bindings).

### Setup for Input Bindings

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./GeneralSetup.md#create-a-sql-server).

- Open your project that you created in [Create a Function Project](./GeneralSetup.md#create-a-function-project) in VS Code
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger -> (Provide a function name) -> Company.namespace -> anonymous
- In the file that opens, replace the `public static async Task<IActionResult> Run` block with the below code.

    ```csharp
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "employees")] HttpRequest req,
        ILogger log,
        [Sql("select * from Employees",
        "SqlConnectionString")]
        IEnumerable<Employee> employee)
    {
        return new OkObjectResult(employee);
    }
    ```

    *In the above, `select * from Employees` is the SQL script run by the input binding. The CommandType on the line below specifies whether the first line is a query or a stored procedure. The next parameter specifies that the app setting that contains the SQL connection string used to connect to the database is "SqlConnectionString." For more information on this, see the [SqlAttribute for Input Bindings](#sqlattribute-for-input-bindings) section*

- Add 'using System.Collections.Generic;' to the namespaces list at the top of the page.
- Currently, there is an error for the IEnumerable. We'll fix this by creating an Employee class.
- Create a new file and call it 'Employee.cs'
- Paste the below in the file. These are the column names of our SQL table. Note that the casing of the Object field names and the table column names must match.

    ```csharp
    namespace Company.Function {
        public class Employee {
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

The input binding executes the `select * from Products where Cost = @Cost` query, returning the result as an `IEnumerable<Product>`, where Product is a user-defined POCO. The *Parameters* argument passes the `{cost}` specified in the URL that triggers the function, `getproducts/{cost}`, as the value of the `@Cost` parameter in the query. *CommandType* is set to `System.Data.CommandType.Text`, since the constructor argument of the binding is a raw query.

```csharp
[FunctionName("GetProducts")]
  public static IActionResult Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts/{cost}")]
      HttpRequest req,
      [Sql("select * from Products where Cost = @Cost",
          "SqlConnectionString",
          parameters: "@Cost={cost}")]
      IEnumerable<Product> products)
  {
      return (ActionResult)new OkObjectResult(products);
  }
```

`Product` is a user-defined POCO that follows the structure of the Products table. It represents a row of the Products table, with field names and types copying those of the Products table schema. For example, if the Products table has three columns of the form

- **ProductId**: int
- **Name**: varchar
- **Cost**: int

Then the `Product` class would look like

```csharp
public class Product
{
    public int ProductId { get; set; }

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
          "SqlConnectionString",
          parameters: "@Cost={cost},@Name=")]
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
          "SqlConnectionString",
          parameters: "@Name={name}")]
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
          "SqlConnectionString",
          System.Data.CommandType.StoredProcedure,
          "@Cost={cost}")]
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
         "SqlConnectionString",
         parameters: "@Cost={cost}")]
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

See [Output Binding Overview](./BindingsOverview.md#output-binding) for general information about the Azure SQL Output binding.

### SqlAttribute for Output Bindings

The [SqlAttribute](https://github.com/Azure/azure-functions-sql-extension/blob/main/src/SqlAttribute.cs) for Output bindings takes two arguments:

- **CommandText**: Represents the name of the table into which rows will be upserted.
- **ConnectionStringSetting**: Specifies the name of the app setting that contains the SQL connection string used to connect to a database. The connection string must follow the format specified [here](https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0).

The following are valid binding types for the rows to be upserted into the table:

- **ICollector&lt;T&gt;/IAsyncCollector&lt;T&gt;**: Each element is a row represented by `T`, where `T` is a user-defined POCO, or Plain Old C# Object. `T` should follow the structure of a row in the queried table. See the [Query String](#query-string) for an example of what `T` should look like.
- **T**: Used when just one row is to be upserted into the table.
- **T[]**: Each element is again a row of the result represented by `T`. This output binding type requires manual instantiation of the array in the function.

The repo contains examples of each of these binding types [here](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-csharp/OutputBindingSamples). A few examples are also included [below](#samples-for-output-bindings).

### Setup for Output Bindings

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./GeneralSetup.md#create-a-sql-server), and that you have the 'Employee.cs' class from the [Setup for Input Bindings](#setup-for-input-bindings) section.

- Open your app in VS Code
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger ->  (Provide a function name) -> Company.namespace is fine -> anonymous
- In the file that opens, replace the `public static async Task<IActionResult> Run` block with the below code

    ```csharp
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addemployees-array")]
        HttpRequest req, ILogger log,
        [Sql("dbo.Employees", "SqlConnectionString")]
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
[Sql("Products", "SqlConnectionString")] ICollector<Product> products)
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
[Sql("Products", "SqlConnectionString")] IAsyncCollector<Product> products)
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
[Sql("dbo.Products", "SqlConnectionString")] out Product[] output)
{
    // Suppose that the ProductId column is the primary key in the Products table, and the
    // table already contains a row with ProductId = 1. In that case, the row will be updated
    // instead of inserted to have values Name = "Cup" and Cost = 2.
    output = new Product[2];
    var product = new Product();
    product.ProductId = 1;
    product.Name = "Cup";
    product.Cost = 2;
    output[0] = product;
    product = new Product();
    product.ProductId = 2;
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
[Sql("Products", "SqlConnectionString")] out Product product)
{
    product = new Product
    {
        Name = req.Query["name"],
        ProductId = int.Parse(req.Query["id"]),
        Cost = int.Parse(req.Query["cost"])
    };
    return new CreatedResult($"/api/addproduct", product);
}
```

## Trigger Binding

See [Trigger Binding Overview](./BindingsOverview.md#trigger-binding) for general information about the Azure SQL Trigger binding.

### SqlTriggerAttribute

The SqlAttribute for Trigger bindings takes two [arguments](https://github.com/Azure/azure-functions-sql-extension/blob/main/src/TriggerBinding/SqlTriggerAttribute.cs)

- **TableName**: Represents the name of the table to be monitored for changes.
- **ConnectionStringSetting**: Specifies the name of the app setting that contains the SQL connection string used to connect to a database. The connection string must follow the format specified [here](https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0).

The trigger binding can bind to type `IReadOnlyList<SqlChange<T>>`:

- **IReadOnlyList<SqlChange\<T\>>**: If there are multiple rows updated in the SQL table, the user function will get invoked with a batch of changes, where each element is a `SqlChange` object. Here `T` is a generic type-argument that can be substituted with a user-defined POCO, or Plain Old C# Object, representing the user table row. The POCO should therefore follow the schema of the queried table. See the [Query String](#query-string) section for an example of what the POCO should look like. The two properties of class `SqlChange<T>` are `Item` of type `T` which represents the table row and `Operation` of type `SqlChangeOperation` which indicates the kind of row operation (insert, update, or delete) that triggered the user function.

Note that for insert and update operations, the user function receives POCO object containing the latest values of table columns. For delete operation, only the properties corresponding to the primary keys of the row are populated.

Any time when the changes happen to the "Products" table, the user function will be invoked with a batch of changes. The changes are processed sequentially, so if there are a large number of changes pending to be processed, the function will be passed a batch containing the earliest changes first.

### Setup for Trigger Bindings

```csharp
[FunctionName("ProductsTrigger")]
public static void Run(
    [SqlTrigger("Products", "SqlConnectionString")]
    IReadOnlyList<SqlChange<Product>> changes,
    ILogger logger)
{
    foreach (SqlChange<Product> change in changes)
    {
        Product product = change.Item;
        logger.LogInformation($"Change operation: {change.Operation}");
        logger.LogInformation($"ProductId: {product.ProductId}, Name: {product.Name}, Cost: {product.Cost}");
    }
}
```

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./GeneralSetup.md#create-a-sql-server), and that you have the 'Employee.cs' file from the [Setup for Input Bindings](#setup-for-input-bindings) section.

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
                [SqlTrigger("[dbo].[Employees]", "SqlConnectionString")]
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
