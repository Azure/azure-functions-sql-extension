# SQL Extension for Azure Functions - Preview

[![Build Status](https://mssqltools.visualstudio.com/CrossPlatBuildScripts/_apis/build/status/SQL%20Bindings/SQL%20Bindings%20-%20Nightly?branchName=main)](https://mssqltools.visualstudio.com/CrossPlatBuildScripts/_build/latest?definitionId=481&branchName=main)

## Introduction

This repository contains extension code for SQL bindings as well as a quick start, tutorial, and samples of how to use them. A high level explanation of the bindings is provided below. Additional information for each is in their respective sample sections.

- **input binding**: takes a SQL query to run on a provided table and returns the output of the query.
- **output binding**: takes a list of rows and upserts them into the user table (i.e. If a row doesn't already exist, it is added. If it does, it is updated).

## Table of Contents

- [SQL Extension for Azure Functions - Preview](#sql-extension-for-azure-functions---preview)
  - [Introduction](#introduction)
  - [Table of Contents](#table-of-contents)
  - [Quick Start](#quick-start)
    - [SQL Setup](#sql-setup)
    - [Set Up Local .NET Function App](#set-up-local-net-function-app)
  - [Tutorials](#tutorials)
    - [Create Azure SQL Database](#create-azure-sql-database)
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
  - [Contributing](#contributing)

## Quick Start

### SQL Setup

This requires already having a SQL database. If you need to create a SQL database, please refer to [Create Azure SQL Database](#Create-Azure-SQL-Database) in the tutorials section.

A primary key must be set in your SQL table before using the bindings. To do this, run the below SQL commands in the SQL query editor.

1. Ensure there are no NULL values in the primary key column. The primary key will usually be an ID column.

    ```sql
    ALTER TABLE ['your table name'] alter column ['column to be primary key'] int NOT NULL
    ```

1. Set primary key column.

    ```sql
    ALTER TABLE ['your table name'] ADD CONSTRAINT PKey PRIMARY KEY CLUSTERED (['column to be primary key']);
    ```

1. Congrats on setting up your database! Now continue to set up your local environment and complete the quick start.

### Set Up Local .NET Function App

These steps can be done in the CLI, Powershell. Completing this section will allow you to begin using the bindings.

1. Add MyGet package feed. If you are running into errors, make sure you have the [.NET sdk](https://dotnet.microsoft.com/download) installed and in your system PATHS.

    ```bash
    dotnet nuget add source https://www.myget.org/F/azure-appservice/api/v3/index.json
    ```

1. Install [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local)

1. Create a function app.

    ```bash
    mkdir MyApp
    cd MyApp
    func init --worker-runtime dotnet
    ```

1. Install the extension.

    ```bash
    dotnet add package Microsoft.Azure.WebJobs.Extensions.Sql --version 1.0.0-preview3
    ```

1. Ensure you have Azure Storage Emulator running. For information on the Azure Storage Emulator, refer [here](https://docs.microsoft.com/azure/storage/common/storage-use-emulator#get-the-storage-emulator)

1. Get your SqlConnectionString. Your connection string can be found in your SQL database resource by going to the left blade and clicking 'Connection strings'. Copy the Connection String.

    (*Note: when pasting in the connection string, you will need to replace part of the connection string where it says '{your_password}' with your Azure SQL Server password*)

1. In 'local.settings.json' in 'Values', verify you have the below. If not, add the below and replace "Your Connection String" with the your connection string from the previous step:

    ```json
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "AzureWebJobsDashboard": "UseDevelopmentStorage=true",
    "SqlConnectionString": "<Your Connection String>"
    ```

1. Verify your host.json looks like the below:

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

### Create Azure SQL Database

We will create a simple Azure SQL Database. For additional reference on Azure SQL Databases, go [here](https://docs.microsoft.com/azure/azure-sql/database/single-database-create-quickstart?tabs=azure-portal).

- Create an Azure SQL Database
  - Make sure you have an Azure subscription. If you don't already have an Azure Subscription, go [here](https://azure.microsoft.com/free/search/?&OCID=AID2100131_SEM_XzK4bAAAAJBpCjfl:20200918000154:s&msclkid=f33d47a9a4ec1c1b6ced18cd9bd2923f&ef_id=XzK4bAAAAJBpCjfl:20200918000154:s&dclid=CKLQqbL28esCFUrBfgod4BIBMA).
  - Navigate to the [Azure portal](https://ms.portal.azure.com/)
  - Click 'Create a resource', then search the marketplace for 'SQL Database' and select it. Provide a 'Subscription', 'Resource Group', and 'Database name.' Under the 'Server' field, click 'Create New'
<kbd>![alt text](/Images/dbSetup.png)</kbd>
  - Fill in the fields of the 'New server' panel. Make sure you know your 'Server admin login' and 'Password' as you will need them later. Click 'OK' at the bottom of the panel.
  - Click 'Review and Create' at the bottom of the page. Then press 'Create.' While you are waiting for your resource to be created, feel free to do the [Set Up Local .NET Function App](#Set-Up-Local-.NET-Function-App) step if you have not done so already and return here when completed.
- Once created, navigate to the SQL Database resource. In the left panel, click 'Query editor'
- Enter your Azure SQL login from when you created the SQL Database.
  - If an error pops up for not being able to open the server, copy the Client IP address in the second sentence of the error message, and click 'set server firewall' at the bottom
  - In the new window, click 'Add Client IP.' This will create an entry
  - In the section with Rule Name, Start IP, and End IP, paste the IP address you just copied into the Start and End IP fields for the entry created in the previous step.
  - Hit 'Save' in the top left and navigate back into the SQL Database login page.
  - Enter your login. You should now be in the Query editor view
- Enter the below script and hit run to create a table. Once created, if you expand the Tables section by clicking the arrow, you should see a table

    ```sql
    CREATE TABLE Employees (
          EmployeeId int,
          FirstName varchar(255),
          LastName varchar(255),
          Company varchar(255),
          Team varchar(255)
    );
    ```

- Enter the blow script and hit run to create an entry in the table. Once created, if you right click your table name and click 'Select Top 1000 Rows', you'll be able to see your entry present.

    ```sql
    INSERT INTO [dbo].[Employees] values (1, 'Hello', 'World', 'Microsoft', 'Functions')
    ```

- Congratulations! You have successfully created an Azure SQL Database! Make sure you complete [Quick Start](#Quick-Start) before continuing to the rest of the tutorial.

### Input Binding Tutorial

Note: This tutorial requires that the Azure SQL database is setup as shown in [Create Azure SQL Database](#Create-Azure-SQL-Database).

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

Note: This tutorial requires that the Azure SQL database is setup as shown in [Create Azure SQL Database](#Create-Azure-SQL-Database), and that you have the 'Employee.cs' class from the [Input Binding Tutorial](#Input-Binding-Tutorial).

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

The input binding takes four arguments

- **CommandText**: Passed as a constructor argument to the binding. Represents either a query string or the name of a stored procedure.
- **CommandType**: Specifies whether CommandText is a query (`System.Data.CommandType.Text`) or a stored procedure (`System.Data.CommandType.StoredProcedure`)
- **Parameters**: The parameters to the query/stored procedure. This string must follow the format "@param1=param1,@param2=param2" where @param1 is the name of the parameter and param1 is the parameter value. Each pair of parameter name, parameter value is separated by a comma. Within each pair, the parameter name and value is separated by an equals sign. This means that neither the parameter name nor value can contain "," or "=". To specify a `NULL` parameter value, do "@param1=null,@param2=param2". To specify an empty string as a value, do "@param1=,@param2=param2", i.e. do not put any text after the equals sign of the corresponding parameter name. This argument is auto-resolvable (see Query String examples).
- **ConnectionStringSetting**: Specifies the name of the app setting that contains the SQL connection string used to connect to a database. The connection string must follow the format specified [here](https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0).

The following are valid binding types for the result of the query/stored procedure execution:

- **IEnumerable<T>**: Each element is a row of the result represented by `T`, where `T` is a user-defined POCO, or Plain Old C# Object. `T` should follow the structure of a row in the queried table. See the [Query String](#query-string) section for an example of what `T` should look like.
- **IAsyncEnumerable<T>**: Each element is again a row of the result represented by `T`, but the rows are retrieved "lazily". A row of the result is only retrieved when `MoveNextAsync` is called on the enumerator. This is useful in the case that the query can return a very large amount of rows.
- **String**: A JSON string representation of the rows of the result (an example is provided [here](https://github.com/Azure/azure-functions-sql-extension/blob/main/samples/SqlExtensionSamples/InputBindingSamples/GetProductsString.cs)).
- **SqlCommand**: The SqlCommand is populated with the appropriate query and parameters, but the associated connection is not opened. It is the responsiblity of the user to execute the command and read in the results. This is useful in the case that the user wants more control over how the results are read in. An example is provided [here](https://github.com/Azure/azure-functions-sql-extension/blob/main/samples/SqlExtensionSamples/InputBindingSamples/GetProductsSqlCommand.cs).

The repo contains examples of each of these binding types [here](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/SqlExtensionSamples/InputBindingSamples). A few examples are also included below.

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

The output binding takes two arguments

- **CommandText**: Passed as a constructor argument to the binding. Represents the name of the table into which rows will be upserted.
- **ConnectionStringSetting**: Specifies the name of the app setting that contains the SQL connection string used to connect to a database. The connection string must follow the format specified [here](https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0).

The following are valid binding types for the rows to be upserted into the table:

- **ICollector<T>/IAsyncCollector<T>**: Each element is a row represented by `T`, where `T` is a user-defined POCO, or Plain Old C# Object. `T` should follow the structure of a row in the queried table. See the [Query String](#query-string) for an example of what `T` should look like.
- **T**: Used when just one row is to be upserted into the table.
- **T[]**: Each element is again a row of the result represented by `T`. This output binding type requires manual instantiation of the array in the function.

The repo contains examples of each of these binding types [here](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/SqlExtensionSamples/OutputBindingSamples). A few examples are also included below.

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

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
