# Azure SQL bindings for Azure Functions - .NET (CSharp Script)

## Table of Contents

- [Azure SQL bindings for Azure Functions - .NET (CSharp Script)](#azure-sql-bindings-for-azure-functions---net-csharp-script)
  - [Table of Contents](#table-of-contents)
  - [CSharp Scripting](#csharp-scripting)
  - [Setup Function Project](#setup-function-project)
  - [Input Binding](#input-binding)
    - [function.json Properties for Input Bindings](#functionjson-properties-for-input-bindings)
    - [Setup for Input Bindings](#setup-for-input-bindings)
    - [Samples for Input Bindings](#samples-for-input-bindings)
      - [Query String](#query-string)
      - [Empty Parameter Value](#empty-parameter-value)
      - [Null Parameter Value](#null-parameter-value)
      - [Stored Procedure](#stored-procedure)
  - [Output Binding](#output-binding)
    - [function.json Properties for Output Bindings](#functionjson-properties-for-output-bindings)
    - [Setup for Output Bindings](#setup-for-output-bindings)
    - [Samples for Output Bindings](#samples-for-output-bindings)
      - [Array](#array)
      - [Single Row](#single-row)
    - [Sample with multiple Bindings](#sample-with-multiple-bindings)
  - [Trigger Binding](#trigger-binding)

## CSharp Scripting

See [How .csx works](https://learn.microsoft.com/azure/azure-functions/functions-reference-csharp?tabs=functionsv2#how-csx-works) for general information and how Azure Functions lets you develop functions using C# script (.csx).

## Setup Function Project

These instructions will guide you through creating your Function Project and adding the SQL binding extension. This only needs to be done once for every function project you create. If you have one created already you can skip this step.

1. Install [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local)

2. Create a function project for .NET:

    ```bash
    mkdir MyApp
    cd MyApp
    func init --worker-runtime dotnet
    ```

3. Initialize a new csx function inside your **MyApp** folder by running below.

    ```bash
    func new --csx
    ```
   Once you execute func new --csx, it will prompt you to select a template using the up/down arrow keys, Please select **HTTP trigger** which prompts for function name, enter your function name **MyCSXFunction** on which it will create a csharp script function scaffolding under **MyApp/MyCSXFunction/**.

4. Enable SQL bindings on the csx function created above. More information can be found in the [Azure SQL bindings for Azure Functions docs](https://aka.ms/sqlbindings).

    Update the `host.json` file inside **MyApp/MyCSXFunction/** to the preview extension bundle.

    ```json
    "extensionBundle": {
        "id": "Microsoft.Azure.Functions.ExtensionBundle.Preview",
        "version": "[4.*, 5.0.0)"
    }
    ```
    Once you have these set up, your project folder should resemble [Folder Structure](https://learn.microsoft.com/azure/azure-functions/functions-reference-csharp?tabs=functionsv2#folder-structure)

## Input Binding

See [Input Binding Overview](./BindingsOverview.md#input-binding) for general information about the Azure SQL Input binding.

### function.json Properties for Input Bindings

The following table explains the binding configuration properties that you set in the *function.json* file.

|function.json property | Description|
|---------|----------------------|
|**type** |  Required. Must be set to `sql`. |
|**direction** | Required. Must be set to `in`. |
|**name** |  Required. The name of the variable that represents the query results in function code. |
| **commandText** | Required. The Transact-SQL query command or name of the stored procedure executed by the binding.  |
| **connectionStringSetting** | Required. The name of an app setting that contains the connection string for the database against which the query or stored procedure is being executed. This value isn't the actual connection string and must instead resolve to an environment variable name.  Optional keywords in the connection string value are [available to refine SQL bindings connectivity](https://aka.ms/sqlbindings#sql-connection-string). |
| **commandType** | Required. A [CommandType](https://learn.microsoft.com/dotnet/api/system.data.commandtype) value, which is [Text](https://learn.microsoft.com/dotnet/api/system.data.commandtype#fields) for a query and [StoredProcedure](https://learn.microsoft.com/dotnet/api/system.data.commandtype#fields) for a stored procedure. |
| **parameters** | Optional. Zero or more parameter values passed to the command during execution as a single string. Must follow the format `@param1=param1,@param2=param2`. Neither the parameter name nor the parameter value can contain a comma (`,`) or an equals sign (`=`). |

### Setup for Input Bindings

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./GeneralSetup.md#create-a-sql-server).

- Open your project that you created in [Create a Function Project](./GeneralSetup.md#create-a-function-project) in VS Code
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger -> (Provide a function name) -> anonymous
- In the folder that is created with the provided function name, open the file (`run.csx`), replace its contents block with the below code.

    ```csharp
    #load "employee.csx"
    #r "Newtonsoft.Json"

    using System.Net;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Primitives;
    using Newtonsoft.Json;
    using System.Collections.Generic;

    public static IActionResult Run(HttpRequest req, ILogger log, IEnumerable<Employee> employees)
    {
        log.LogInformation("CSX HTTP trigger function processed a request.");
        return new OkObjectResult(employees);
    }
    ```

- We also need to add the SQL input binding for the `employees` parameter. Open the function.json file.
- Paste the below in the file as an additional entry to the "bindings": [] array.

    ```json
    {
      "name": "employees",
      "type": "sql",
      "direction": "in",
      "commandText": "select * from Employees",
      "commandType": "Text",
      "connectionStringSetting": "SqlConnectionString"
    }
    ```

    *In the above, `select * from Employees` is the SQL script run by the input binding. The CommandType on the line below specifies whether the first line is a query or a stored procedure. On the next line, the ConnectionStringSetting specifies that the app setting that contains the SQL connection string used to connect to the database is "SqlConnectionString." For more information on this, see the [function.json Properties for Input Bindings](#functionjson-properties-for-input-bindings) section*

- Open the local.settings.json file, and in the brackets for "Values," verify there is a 'SqlConnectionString.' If not, add it.
- Hit 'F5' to run your code. This will start up the Functions Host with a local HTTP Trigger and SQL Input Binding.
- Click the link that appears in your terminal.
- You should see your database output in the browser window.
- Congratulations! You have successfully created your first SQL input binding!

### Samples for Input Bindings
The database scripts used for the following samples can be found [here](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/Database).

#### Query String

See the [GetProducts](https://github.com/Azure/azure-functions-sql-extension/blob/main/samples/samples-csx/InputBindingSamples/GetProducts) sample

#### Empty Parameter Value

See the [GetProductsNameEmpty](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-csx/InputBindingSamples/GetProductsNameEmpty) sample

#### Null Parameter Value

See the [GetProductsNameNull](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-csx/InputBindingSamples/GetProductsNameNull) sample

#### Stored Procedure

See the [GetProductsStoredProcedure](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-csx/InputBindingSamples/GetProductsStoredProcedure) sample

## Output Binding

See [Output Binding Overview](./BindingsOverview.md#output-binding) for general information about the Azure SQL Output binding.

### function.json Properties for Output Bindings

The following table explains the binding configuration properties that you set in the *function.json* file.

|function.json property | Description|
|---------|----------------------|
|**type** | Required. Must be set to `sql`.|
|**direction** | Required. Must be set to `out`. |
|**name** | Required. The name of the variable that represents the entity in function code. |
| **commandText** | Required. The name of the table being written to by the binding.  |
| **connectionStringSetting** | Required. The name of an app setting that contains the connection string for the database to which data is being written. This isn't the actual connection string and must instead resolve to an environment variable. Optional keywords in the connection string value are [available to refine SQL bindings connectivity](https://aka.ms/sqlbindings#sql-connection-string). |

### Setup for Output Bindings

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./GeneralSetup.md#create-a-sql-server).

- Open your app in VS Code
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger ->  (Provide a function name) -> anonymous
- In the folder that is created with the provided function name, open the file (`run.csx`), replace its contents block with the below code. Note that the casing of the Object field names and the table column names must match.

    ```csharp
    #load "employee.csx"
    #r "Newtonsoft.Json"
    #r "Microsoft.Azure.WebJobs.Extensions.Sql"

    using System.Net;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Primitives;
    using Newtonsoft.Json;

    public static Product Run(HttpRequest req, ILogger log, [Sql("dbo.Employees", "SqlConnectionString")] out Employee employee)
    {
        log.LogInformation("CSX HTTP trigger function processed a request.");


        string requestBody = new StreamReader(req.Body).ReadToEnd();
        employee = JsonConvert.DeserializeObject<Employee>(requestBody);

        string responseMessage = string.IsNullOrEmpty(employee.Name)
            ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                    : $"Hello, {employee.Name}. This HTTP triggered function executed successfully.";

        return employee;
    }
    ```

- We also need to add the SQL output binding for the `bindings.employee` property. Open the function.json file.
- Paste the below in the file as an additional entry to the "bindings": [] array.

    ```json
    {
      "name": "employee",
      "type": "sql",
      "direction": "out",
      "commandText": "dbo.Employees",
      "connectionStringSetting": "SqlConnectionString"
    }
    ```

    *In the above, "dbo.Employees" is the name of the table our output binding is upserting into. The line below is similar to the input binding and specifies where our SqlConnectionString is. For more information on this, see the [function.json Properties for Output Bindings](#functionjson-properties-for-output-bindings) section*

- Open the local.settings.json file, and in the brackets for "Values," verify there is a 'SqlConnectionString.' If not, add it.
- Hit 'F5' to run your code. Click the link to upsert the output array values in your SQL table. Your upserted values should launch in the browser.
- Congratulations! You have successfully created your first SQL output binding!

### Samples for Output Bindings

#### Array

See the [AddProductsArray](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-csx/OutputBindingSamples/AddProductsArray) sample

#### Single Row

See the [AddProduct](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-csx/OutputBindingSamples/AddProduct) sample

### Sample with multiple Bindings

See the [GetAndAddProducts](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-csx/InputBindingSamples/GetAndAddProducts) sample


## Trigger Binding

> Trigger binding support is only available for in-proc C# functions at present.
