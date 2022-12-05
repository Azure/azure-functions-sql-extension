# Azure SQL bindings for Azure Functions - .NET (Isolated Process)

## Table of Contents
- [Azure SQL bindings for Azure Functions - .NET (Isolated Process)](#azure-sql-bindings-for-azure-functions---net-isolated-process)
  - [Table of Contents](#table-of-contents)
  - [Binding Model](#binding-model)
  - [Key differences with .NET (Isolated Process)](#key-differences-with-net-isolated-process)
  - [Input Binding](#input-binding)
    - [SqlInputAttribute for Input Bindings](#sqlinputattribute-for-input-bindings)
    - [Setup for Input Bindings](#setup-for-input-bindings)
    - [Samples for Input Bindings](#samples-for-input-bindings)
      - [Query String](#query-string)
      - [Empty Parameter Value](#empty-parameter-value)
      - [Null Parameter Value](#null-parameter-value)
      - [Stored Procedure](#stored-procedure)
      - [IAsyncEnumerable](#iasyncenumerable)
  - [Output Binding](#output-binding)
    - [SqlOutputAttribute for Output Bindings](#sqloutputattribute-for-output-bindings)
    - [Setup for Output Bindings](#setup-for-output-bindings)
    - [Samples for Output Bindings](#samples-for-output-bindings)
      - [Array](#array)
      - [Single Row](#single-row)
  - [Trigger Binding](#trigger-binding)

## Binding Model

.NET Isolated introduces a new binding model, slightly different from the binding model exposed in .NET Core 3 Azure Functions. More information can be [found here](https://github.com/Azure/azure-functions-dotnet-worker/wiki/.NET-Worker-bindings). Please review our samples for usage information.

## Key differences with .NET (Isolated Process)
Please refer to the functions documentation [here](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-in-process-differences)
- Because .NET isolated projects run in a separate worker process, bindings can't take advantage of rich binding classes, such as ICollector<T>, IAsyncCollector<T>, and CloudBlockBlob.
- There's also no direct support for types inherited from underlying service SDKs, such as SqlCommand. Instead, bindings rely on strings, arrays, and serializable types, such as plain old class objects (POCOs).
- For HTTP triggers, you must use HttpRequestData and HttpResponseData to access the request and response data. This is because you don't have access to the original HTTP request and response objects when running out-of-process.

## Input Binding

See [Input Binding Overview](./BindingsOverview.md#input-binding) for general information about the Azure SQL Input binding.

### SqlInputAttribute for Input Bindings

The [SqlInputAttribute](https://github.com/Azure/azure-functions-sql-extension/blob/main/Worker.Extension.Sql/src/SqlInputAttribute.cs) takes four arguments:

- **CommandText**: Passed as a constructor argument to the binding. Represents either a query string or the name of a stored procedure.
- **CommandType**: Specifies whether CommandText is a query (`System.Data.CommandType.Text`) or a stored procedure (`System.Data.CommandType.StoredProcedure`)
- **Parameters**: The parameters to the query/stored procedure. This string must follow the format "@param1=param1,@param2=param2" where @param1 is the name of the parameter and param1 is the parameter value. Each pair of parameter name, parameter value is separated by a comma. Within each pair, the parameter name and value is separated by an equals sign. This means that neither the parameter name nor value can contain "," or "=". To specify a `NULL` parameter value, do "@param1=null,@param2=param2". To specify an empty string as a value, do "@param1=,@param2=param2", i.e. do not put any text after the equals sign of the corresponding parameter name. This argument is auto-resolvable (see Query String examples).
- **ConnectionStringSetting**: Specifies the name of the app setting that contains the SQL connection string used to connect to a database. The connection string must follow the format specified [here](https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0).

The following are valid binding types for the result of the query/stored procedure execution:

- **IEnumerable&lt;T&gt;**: Each element is a row of the result represented by `T`, where `T` is a user-defined POCO, or Plain Old C# Object. `T` should follow the structure of a row in the queried table. See the [Query String](#query-string) section for an example of what `T` should look like.
- **IAsyncEnumerable&lt;T&gt;**: Each element is again a row of the result represented by `T`, but the rows are retrieved "lazily". A row of the result is only retrieved when `MoveNextAsync` is called on the enumerator. This is useful in the case that the query can return a very large amount of rows.
- **String**: A JSON string representation of the rows of the result (an example is provided [here](https://github.com/Azure/azure-functions-sql-extension/blob/main/samples/samples-outpofproc/InputBindingSamples/GetProductsString.cs)).

**Note**: There's also no direct support for types inherited from underlying service SDKs, such as SqlCommand. Instead, bindings rely on strings, arrays, and serializable types, such as plain old class objects (POCOs).

The repo contains examples of each of these binding types [here](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-outofproc/InputBindingSamples). A few examples are also included [below](#samples-for-input-bindings).

### Setup for Input Bindings

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./QuickStart.md#create-a-sql-server).

- Open your app that you created in [Create a Function App](./QuickStart.md#create-a-function-app) in VS Code
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger -> (Provide a function name) -> Company.namespace -> anonymous
- In the file that opens, replace the `public static async Task<IActionResult> Run` block with the below code.

    ```csharp
    [Function("GetEmployees")]
    public static async IEnumerable<Employee> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "employees")] HttpRequest req,
        ILogger log,
        [SqlInput("select * from Employees",
        CommandType = System.Data.CommandType.Text,
        ConnectionStringSetting = "SqlConnectionString")]
        IEnumerable<Employee> employees)
    {
        return employees;
    }
    ```

    *In the above, "select * from Employees" is the SQL script run by the input binding. The CommandType on the line below specifies whether the first line is a query or a stored procedure. On the next line, the ConnectionStringSetting specifies that the app setting that contains the SQL connection string used to connect to the database is "SqlConnectionString." For more information on this, see the [SqlInputAttribute for Input Bindings](#sqlinputattribute-for-input-bindings) section*
- Add 'using Microsoft.Azure.Functions.Worker.Extension.Sql;' for using *SqlInput*, the out of proc sql input binding.
- Add 'using System.Collections.Generic;' to the namespaces list at the top of the page.
- Currently, there is an error for the IEnumerable. We'll fix this by creating an Employee class.
- Create a new file and call it 'Employee.cs'
- Paste the below in the file. These are the column names of our SQL table.

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
- Congratulations! You have successfully created your first out of process SQL input binding!

### Samples for Input Bindings

#### Query String

The input binding executes the "select * from Products where Cost = @Cost" query, returning the result as an `IEnumerable<Product>`, where Product is a user-defined POCO. The *Parameters* argument passes the `{cost}` specified in the URL that triggers the function, `getproducts/{cost}`, as the value of the `@Cost` parameter in the query. *CommandType* is set to `System.Data.CommandType.Text`, since the constructor argument of the binding is a raw query.

```csharp
  [Function("GetProducts")]
  public static IEnumerable<Product> Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts/{cost}")]
      HttpRequest req,
      [SqlInput("select * from Products where Cost = @Cost",
          CommandType = System.Data.CommandType.Text,
          Parameters = "@Cost={cost}",
          ConnectionStringSetting = "SqlConnectionString")]
      IEnumerable<Product> products)
  {
      return products;
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
  [Function("GetProductsNameEmpty")]
  public static IEnumerable<Product> Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-nameempty/{cost}")]
      HttpRequest req,
      [SqlInput("select * from Products where Cost = @Cost and Name = @Name",
          CommandType = System.Data.CommandType.Text,
          Parameters = "@Cost={cost},@Name=",
          ConnectionStringSetting = "SqlConnectionString")]
      IEnumerable<Product> products)
  {
      return products;
  }
  ```

#### Null Parameter Value

If the `{name}` specified in the `getproducts-namenull/{name}` URL is "null", the query returns all rows for which the Name column is `NULL`. Otherwise, it returns all rows for which the value of the Name column matches the string passed in `{name}`

```csharp
  [Function("GetProductsNameNull")]
  public static IEnumerable<Product> Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-namenull/{name}")]
      HttpRequest req,
      [SqlInput("if @Name is null select * from Products where Name is null else select * from Products where @Name = name",
          CommandType = System.Data.CommandType.Text,
          Parameters = "@Name={name}",
          ConnectionStringSetting = "SqlConnectionString")]
      IEnumerable<Product> products)
  {
      return products;
  }
```

#### Stored Procedure

`SelectsProductCost` is the name of a procedure stored in the user's database. In this case, *CommandType* is `System.Data.CommandType.StoredProcedure`. The parameter value of the `@Cost` parameter in the procedure is once again the `{cost}` specified in the `getproducts-storedprocedure/{cost}` URL.

```csharp
  [Function("GetProductsStoredProcedure")]
  public static IActionResult Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-storedprocedure/{cost}")]
      HttpRequest req,
      [SqlInput("SelectProductsCost",
          CommandType = System.Data.CommandType.StoredProcedure,
          Parameters = "@Cost={cost}",
          ConnectionStringSetting = "SqlConnectionString")]
      IEnumerable<Product> products)
  {
      return products;
  }
```

#### IAsyncEnumerable

Using the `IAsyncEnumerable` binding generally requires that the `Run` function be `async`. It is also important to call `DisposeAsync` at the end of function execution to make sure all resources used by the enumerator are freed.

```csharp
[Function("GetProductsAsyncEnumerable")]
public static async Task<List<Product>> Run(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-async/{cost}")]
     HttpRequest req,
    [SqlInput("select * from Products where cost = @Cost",
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
    return productList;
}
```

## Output Binding

See [Output Binding Overview](./BindingsOverview.md#output-binding) for general information about the Azure SQL Output binding.

### SqlOutputAttribute for Output Bindings

The [SqlOutputAttribute](https://github.com/Azure/azure-functions-sql-extension/blob/main/Worker.Extension.Sql/src/SqlOutputAttribute.cs) takes two arguments:

- **CommandText**: Passed as a constructor argument to the binding. Represents the name of the table into which rows will be upserted.
- **ConnectionStringSetting**: Specifies the name of the app setting that contains the SQL connection string used to connect to a database. The connection string must follow the format specified [here](https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0).

The following are valid binding types for the rows to be upserted into the table:

Each element is a row represented by `T`, where `T` is a user-defined POCO, or Plain Old C# Object. `T` should follow the structure of a row in the queried table. See the [Query String](#query-string) for an example of what `T` should look like.
- **T**: Used when just one row is to be upserted into the table.
- **T[]**: Each element is again a row of the result represented by `T`. This output binding type requires manual instantiation of the array in the function.
**Note**: As stated in the functions [documentation](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide#output-bindings)
- Because .NET isolated projects run in a separate worker process, bindings can't take advantage of rich binding classes, such as ICollector<T>, IAsyncCollector<T>, and CloudBlockBlob.
The repo contains examples of each of these binding types [here](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-outofproc/OutputBindingSamples). A few examples are also included [below](#samples-for-output-bindings).

### Setup for Output Bindings

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./QuickStart.md#create-a-sql-server), and that you have the 'Employee.cs' class from the [Setup for Input Bindings](#setup-for-input-bindings) section.

- Open your app in VS Code
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger ->  (Provide a function name) -> Company.namespace is fine -> anonymous
- In the file that opens, replace the `public static async Task<IActionResult> Run` block with the below code

    ```csharp
    [Function("AddEmployees")]
    [SqlOutput("dbo.Employees", ConnectionStringSetting = "SqlConnectionString")]
    public static Employee[] Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addemployees-array")]
        HttpRequestData req)
    {
        Employee[] output = new Employee[]
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

        return output;
    }
    ```

    *In the above, "dbo.Employees" is the name of the table our output binding is upserting into. The line below is similar to the input binding and specifies where our SqlConnectionString is. For more information on this, see the [SqloutputAttribute for Output Bindings](#sqloutputattribute-for-output-bindings) section*

- Hit 'F5' to run your code. Click the link to upsert the output array values in your SQL table. Your upserted values should launch in the browser.
- Congratulations! You have successfully created your first out of process SQL output binding!

### Samples for Output Bindings

#### Array

This output binding type requires the product array to be passed in the request body as JSON. Note also that the `Product[]` array being upserted must be returned by output binding function.

``` csharp
[Function("AddProductsArray")]
[SqlOutput("dbo.Products", ConnectionStringSetting = "SqlConnectionString")]
public static async Task<Product[]> Run(
[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproducts-array")]
    HttpRequestData req)
{
    // Upsert the products, which will insert them into the Products table if the primary key (ProductId) for that item doesn't exist.
     // If it does then update it to have the new name and cost
     Product[] prod = await req.ReadFromJsonAsync<Product[]>();
     return prod;
}
```

#### Single Row

```csharp
[Function("AddProduct")]
[SqlOutput("dbo.Products", ConnectionStringSetting = "SqlConnectionString")]
public static Task<Product> Run(
[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct")]
    HttpRequestData req)
{
    Product prod = await req.ReadFromJsonAsync<Product>();
    return prod;
}
```

## Trigger Binding

> Trigger binding support is only available for in-proc C# functions at present.