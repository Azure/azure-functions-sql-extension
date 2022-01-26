# Azure SQL binding for Azure Functions - Preview

[![Build Status](https://mssqltools.visualstudio.com/CrossPlatBuildScripts/_apis/build/status/SQL%20Bindings/SQL%20Bindings%20-%20Nightly?branchName=main)](https://mssqltools.visualstudio.com/CrossPlatBuildScripts/_build/latest?definitionId=481&branchName=main)

## Introduction

This repository contains the Azure SQL binding for Azure Functions extension code as well as a quick start tutorial and samples illustrating how to use the binding in different ways.  A high level explanation of the bindings is provided below. Additional information for each is in their respective sample sections.

- **Input Binding**: takes a SQL query to run and returns the output of the query in the function.
- **Output Binding**: takes a list of rows and upserts them into the user table (i.e. If a row doesn't already exist, it is added. If it does, it is updated).

Further information on the Azure SQL binding for Azure Functions is also available in the [Azure Functions docs](https://docs.microsoft.com/azure/azure-functions/functions-bindings-azure-sql).

## Table of Contents

- [Azure SQL binding for Azure Functions - Preview](#azure-sql-binding-for-azure-functions---preview)
  - [Introduction](#introduction)
  - [Table of Contents](#table-of-contents)
  - [Quick Start](#quick-start)
    - [Create a SQL Server](#create-a-sql-server)
      - [Docker container](#docker-container)
      - [Azure SQL Database](#azure-sql-database)
    - [SQL Setup](#sql-setup)
    - [Create .NET Function App](#create-net-function-app)
    - [Configure Function App](#configure-function-app)
  - [Tutorials](#tutorials)
    - [Input Binding Tutorial](#input-binding-tutorial)
    - [Output Binding Tutorial](#output-binding-tutorial)
  - [More Samples](#more-samples)
    - [Input Binding](#input-binding)
      - [Query String](#query-string)
      - [Empty Parameter Value](#empty-parameter-value)
      - [Null Parameter Value](#null-parameter-value)
      - [Stored Procedure](#stored-procedure)
      - [IAsyncEnumerable](#iasyncenumerable)
    - [Output Binding](#output-binding)
      - [ICollector<T>/IAsyncCollector<T>](#icollectortiasynccollectort)
      - [Array](#array)
      - [Single Row](#single-row)
      - [Primary Keys and Identity Columns](#primary-keys-and-identity-columns)
  - [Known Issues](#known-issues)
  - [Telemetry](#telemetry)
  - [Trademarks](#trademarks)

## Quick Start

### Create a SQL Server

First you'll need a SQL server for the bindings to connect to. If you already have your own set up then you can skip this step, otherwise pick from one of the below options.

#### Docker container

SQL Server on Docker makes it easy to set up and connect to a locally hosted instance of SQL Server. Instructions for getting started can be found [here](https://docs.microsoft.com/sql/linux/sql-server-linux-docker-container-deployment).

#### Azure SQL Database

Azure SQL Database is a fully managed platform as a service (PaaS) database engine that runs the latest stable version of the Microsoft SQL Server database engine. Instructions for getting started can be found [here](https://docs.microsoft.com/azure/azure-sql/database/single-database-create-quickstart).


### SQL Setup

Next you'll configure your SQL Server database for use with Azure SQL binding for Azure Functions.

This will require connecting to and running queries - you can use [Azure Data Studio](https://docs.microsoft.com/sql/azure-data-studio/download-azure-data-studio) or the [MSSQL for VS Code Extension](https://docs.microsoft.com/sql/tools/visual-studio-code/sql-server-develop-use-vscode) to do this.

1. First you'll need a table to run queries against. If you already have one you'd like to use then you can skip this step.

    Otherwise connect to your database and run the following query to create a simple table to start with.

```sql
CREATE TABLE Employees (
        EmployeeId int,
        FirstName varchar(255),
        LastName varchar(255),
        Company varchar(255),
        Team varchar(255)
);
```

2. Next a primary key must be set in your SQL table before using the bindings. To do this, run the queries below, replacing the placeholder values for your table and column.

```sql
ALTER TABLE ['{table_name}'] ALTER COLUMN ['{primary_key_column_name}'] int NOT NULL

ALTER TABLE ['{table_name}'] ADD CONSTRAINT PKey PRIMARY KEY CLUSTERED (['{primary_key_column_name}']);
```


### Create .NET Function App

Now you will need a .NET Function App to add the binding to. If you have one created already you can skip this step.

These steps can be done in the Terminal/CLI or with PowerShell.

1. Install [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local)

2. Create a function app.

    ```bash
    mkdir MyApp
    cd MyApp
    func init --worker-runtime dotnet
    ```

**Note**: Other languages are not supported at this time

1. Install the extension.

    ```powershell
    dotnet add package Microsoft.Azure.WebJobs.Extensions.Sql --prerelease
    ```

### Configure Function App

Once you have your Function App you need to configure it for use with Azure SQL bindings for Azure Functions.

1. Ensure you have Azure Storage Emulator running. This is specific to the sample functions in this repository with a non-HTTP trigger. For information on the Azure Storage Emulator, refer to the docs on its use in [functions local development](https://docs.microsoft.com/azure/azure-functions/functions-app-settings#azurewebjobsstorage) and [installation](https://docs.microsoft.com/azure/storage/common/storage-use-emulator#get-the-storage-emulator).

1. Get your SQL connection string

   <details>
   <summary>Local SQL Server</summary>
   - Use this connection string, replacing the placeholder values for the database and password.</br>
    </br>
    <code>Server=localhost;Initial Catalog={db_name};Persist Security Info=False;User ID=sa;Password={your_password};</code>
   </details>

   <details>
   <summary>Azure SQL Server</summary>
   - Browse to the SQL Database resource in the [Azure portal](https://ms.portal.azure.com/)</br>
   - In the left blade click on the <b>Connection Strings</b> tab</br>
   - Copy the <b>SQL Authentication</b> connection string</br>
    </br>
    (<i>Note: when pasting in the connection string, you will need to replace part of the connection string where it says '{your_password}' with your Azure SQL Server password</i>)
   </details>

1. Open the generated `local.settings.json` file and in the `Values` section verify you have the below. If not, add the below and replace `{connection_string}` with the your connection string from the previous step:

    ```json
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "AzureWebJobsDashboard": "UseDevelopmentStorage=true",
    "SqlConnectionString": "{connection_string}"
    ```

1. Verify your `host.json` looks like the below:

    ```json
    {
        "version": "2.0",
        "logging": {
            "applicationInsights": {
                "samplingExcludedTypes": "Request",
                "samplingSettings": {
                    "isEnabled": true
                }
            }
        }
    }
    ```

1. You have setup your local environment and are now ready to create your first SQL bindings! Continue to the [input](#Input-Binding-Tutorial) and [output](#Output-Binding-Tutorial) binding tutorials, or refer to [More Samples](#More-Samples) for information on how to use the bindings and explore on your own.

## Tutorials

### Input Binding Tutorial

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](#Create-a-SQL-Server).

- Open your app that you created in 'Set Up Your Local Environment' in VSCode
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger -> (Provide a function name) -> Company.namespace -> anonymous
- In the file that opens, replace the 'public static async Task< IActionResult > Run' block with the below code.

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

    *In the above, "select * from Employees" is the SQL script run by the input binding. The CommandType on the line below specifies whether the first line is a query or a stored procedure. On the next line, the ConnectionStringSetting specifies that the app setting that contains the SQL connection string used to connect to the database is "SqlConnectionString." For more information on this, see the [Input Binding](#Input-Binding) section*

- Add 'using System.Collections.Generic;' to the namespaces list at the top of the page.
- Currently, there is an error for the IEnumerable. We'll fix this by creating an Employee class.
- Create a new file and call it 'Employee.cs'
- Paste the below in the file. These are the column values of our SQL Database table.

    ```csharp
    namespace Company.Function {
        public class Employee{
            public int EmployeeId { get; set; }
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string Company { get; set; }
            public string Team { get; set; }
        }
    }
    ```

- Navigate back to your HttpTrigger file. We can ignore the 'Run' warning for now.
- Open the local.settings.json file, and in the brackets for "Values," verify there is a 'SqlConnectionString.' If not, add it.
- Hit 'F5' to run your code. This will start up the Functions Host with a local HTTP Trigger and SQL Input Binding.
- Click the link that appears in your terminal.
- You should see your database output in the browser window.
- Congratulations! You have successfully created your first SQL input binding! Checkout [Input Binding](#Input-Binding) for more information on how to use it and explore on your own!

### Output Binding Tutorial

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](#Create-a-SQL-Server), and that you have the 'Employee.cs' class from the [Input Binding Tutorial](#Input-Binding-Tutorial).

- Open your app in VSCode
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger ->  (Provide a function name) -> Company.namespace is fine -> anonymous
- In the file which opens, replace the 'public static async Task<IActionResult> Run' block with the below code

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

    *In the above, "dbo.Employees" is the name of the table our output binding is upserting into. The line below is similar to the input binding and specifies where our SqlConnectionString is. For more information on this, see the [Output Binding](#Output-Binding) section*

- Hit 'F5' to run your code. Click the link to upsert the output array values in your SQL table. Your upserted values should launch in the browser.
- Congratulations! You have successfully created your first SQL output binding! Checkout [Output Binding](#Output-Binding) for more information on how to use it and explore on your own!

## More Samples

### Input Binding

The input binding takes four [arguments](https://github.com/Azure/azure-functions-sql-extension/blob/main/src/SqlAttribute.cs):

- **CommandText**: Passed as a constructor argument to the binding. Represents either a query string or the name of a stored procedure.
- **CommandType**: Specifies whether CommandText is a query (`System.Data.CommandType.Text`) or a stored procedure (`System.Data.CommandType.StoredProcedure`)
- **Parameters**: The parameters to the query/stored procedure. This string must follow the format "@param1=param1,@param2=param2" where @param1 is the name of the parameter and param1 is the parameter value. Each pair of parameter name, parameter value is separated by a comma. Within each pair, the parameter name and value is separated by an equals sign. This means that neither the parameter name nor value can contain "," or "=". To specify a `NULL` parameter value, do "@param1=null,@param2=param2". To specify an empty string as a value, do "@param1=,@param2=param2", i.e. do not put any text after the equals sign of the corresponding parameter name. This argument is auto-resolvable (see Query String examples).
- **ConnectionStringSetting**: Specifies the name of the app setting that contains the SQL connection string used to connect to a database. The connection string must follow the format specified [here](https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0).

The following are valid binding types for the result of the query/stored procedure execution:

- **IEnumerable<T>**: Each element is a row of the result represented by `T`, where `T` is a user-defined POCO, or Plain Old C# Object. `T` should follow the structure of a row in the queried table. See the [Query String](#query-string) section for an example of what `T` should look like.
- **IAsyncEnumerable<T>**: Each element is again a row of the result represented by `T`, but the rows are retrieved "lazily". A row of the result is only retrieved when `MoveNextAsync` is called on the enumerator. This is useful in the case that the query can return a very large amount of rows.
- **String**: A JSON string representation of the rows of the result (an example is provided [here](https://github.com/Azure/azure-functions-sql-extension/blob/main/samples/InputBindingSamples/GetProductsString.cs)).
- **SqlCommand**: The SqlCommand is populated with the appropriate query and parameters, but the associated connection is not opened. It is the responsiblity of the user to execute the command and read in the results. This is useful in the case that the user wants more control over how the results are read in. An example is provided [here](https://github.com/Azure/azure-functions-sql-extension/blob/main/samples/InputBindingSamples/GetProductsSqlCommand.cs).

The repo contains examples of each of these binding types [here](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/InputBindingSamples). A few examples are also included below.

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

### Output Binding

The output binding takes a list of rows to be upserted into a user table. If the primary key value of the row already exists in the table, the row is interpreted as an update, meaning that the values of the other columns in the table for that primary key are updated. If the primary key value does not exist in the table, the row is interpreted as an insert. The upserting of the rows is batched by the output binding code.

The output binding takes two [arguments](https://github.com/Azure/azure-functions-sql-extension/blob/main/src/SqlAttribute.cs):

- **CommandText**: Passed as a constructor argument to the binding. Represents the name of the table into which rows will be upserted.
- **ConnectionStringSetting**: Specifies the name of the app setting that contains the SQL connection string used to connect to a database. The connection string must follow the format specified [here](https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0).

The following are valid binding types for the rows to be upserted into the table:

- **ICollector<T>/IAsyncCollector<T>**: Each element is a row represented by `T`, where `T` is a user-defined POCO, or Plain Old C# Object. `T` should follow the structure of a row in the queried table. See the [Query String](#query-string) for an example of what `T` should look like.
- **T**: Used when just one row is to be upserted into the table.
- **T[]**: Each element is again a row of the result represented by `T`. This output binding type requires manual instantiation of the array in the function.

The repo contains examples of each of these binding types [here](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/OutputBindingSamples). A few examples are also included below.

#### ICollector<T>/IAsyncCollector<T>

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

#### Primary Keys and Identity Columns

Normally Output Bindings require two things :

1. The table being upserted to contains a Primary Key constraint (composed of one or more columns)
2. Each of those columns must be present in the POCO object used in the attribute

If either of these are false then an error will be thrown.

This changes if one of the primary key columns is an identity column though. In that case there are two options based on how the function defines the output object:

1. If the identity column isn't included in the output object then a straight insert is always performed with the other column values. See [AddProductWithIdentityColumn](./samples/OutputBindingSamples/AddProductWithIdentityColumn.cs) for an example.
2. If the identity column is included (even if it's an optional nullable value) then a merge is performed similar to what happens when no identity column is present. This merge will either insert a new row or update an existing row based on the existence of a row that matches the primary keys (including the identity column). See [AddProductWithIdentityColumnIncluded](./samples/OutputBindingSamples/AddProductWithIdentityColumnIncluded.cs) for an example.

## Known Issues

- Output bindings against tables with columns of data types `NTEXT`, `TEXT`, or `IMAGE` are not supported and data upserts will fail. These types [will be removed](https://docs.microsoft.com/sql/t-sql/data-types/ntext-text-and-image-transact-sql) in a future version of SQL Server and are not compatible with the `OPENJSON` function used by this Azure Functions binding.

## Telemetry

This extension collect usage data in order to help us improve your experience. The data is anonymous and doesn't include any personal information. You can opt-out of telemetry by setting the `AZUREFUNCTIONS_SQLBINDINGS_TELEMETRY_OPTOUT` environment variable or the `AzureFunctionsSqlBindingsTelemetryOptOut` app setting (in your `*.settings.json` file) to '1', 'true' or 'yes';

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft’s Trademark & Brand Guidelines](https://www.microsoft.com/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party’s policies.
