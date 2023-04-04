# Azure SQL bindings for Azure Functions - Python

## Table of Contents

- [Azure SQL bindings for Azure Functions - Python](#azure-sql-bindings-for-azure-functions---python)
  - [Table of Contents](#table-of-contents)
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
  - [Python V2 Model](#python-v2-model)

## Setup Function Project

These instructions will guide you through creating your Function Project and adding the SQL binding extension. This only needs to be done once for every function project you create. If you have one created already you can skip this step.

1. Install [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local) (version >= 4.0.5030)

2. Create a Function Project for Python:

    ```bash
    mkdir MyApp
    cd MyApp
    func init --worker-runtime python
    ```

3. Enable SQL bindings on the function project. More information can be found in the [Azure SQL bindings for Azure Functions docs](https://aka.ms/sqlbindings).

    Update the `host.json` file to the preview extension bundle.

    ```json
    "extensionBundle": {
        "id": "Microsoft.Azure.Functions.ExtensionBundle.Preview",
        "version": "[4.*, 5.0.0)"
    }
    ```

## Input Binding

See [Input Binding Overview](./BindingsOverview.md#input-binding) for general information about the Azure SQL Input binding.

### function.json Properties for Input Bindings

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
- In the file that opens (`__init__.py`), replace the generated function with the following

    ```python
    import azure.functions as func
    import json

    def main(req: func.HttpRequest, employees: func.SqlRowList) -> func.HttpResponse:
        rows = list(map(lambda r: json.loads(r.to_json()), employees))

        return func.HttpResponse(
            json.dumps(rows),
            status_code=200,
            mimetype="application/json"
        )
    ```

- Add an import json statement to the top of the file.
- We also need to add the SQL input binding for the `employees` parameter. Open the function.json file.
- Paste the below in the file as an additional entry to the "bindings": [] array.

    ```json
    {
      "name": "employees",
      "type": "sql",
      "direction": "in",
      "commandText": "SELECT * FROM Employees",
      "connectionStringSetting": "SqlConnectionString"
    }
    ```

    *In the above, `SELECT * FROM Employees` is the SQL script run by the input binding. The CommandType on the line below specifies whether the first line is a query or a stored procedure. On the next line, the ConnectionStringSetting specifies that the app setting that contains the SQL connection string used to connect to the database is "SqlConnectionString." For more information on this, see the [function.json Properties for Input Bindings](#functionjson-properties-for-input-bindings) section*

- Open the local.settings.json file, and in the brackets for "Values," verify there is a 'SqlConnectionString.' If not, add it.
- Hit 'F5' to run your code. This will start up the Functions Host with a local HTTP Trigger and SQL Input Binding.
- Click the link that appears in your terminal.
- You should see your database output in the browser window.
- Congratulations! You have successfully created your first SQL input binding!

### Samples for Input Bindings
The database scripts used for the following samples can be found [here](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/Database).

#### Query String

See the [GetProducts](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-python/GetProducts) sample

#### Empty Parameter Value

See the [GetProductsNameEmpty](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-python/GetProductsNameEmpty) sample

#### Null Parameter Value

See the [GetProductsNameNull](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-python/GetProductsNameNull) sample

#### Stored Procedure

See the [GetProductsStoredProcedure](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-python/GetProductsStoredProcedure) sample

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

- Open your project in VS Code
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger ->  (Provide a function name) -> anonymous
- In the file that opens (`__init__.py`), replace the `def main(req: func.HttpRequest) -> func.HttpResponse:` block with the below code. Note that the casing of the Object field names and the table column names must match.

    ```python
    def main(req: func.HttpRequest, employee: func.Out[func.SqlRow]) -> func.HttpResponse:
        newEmployee = {
            EmployeeId = 1,
            FirstName = "Hello",
            LastName = "World",
            Company = "Microsoft",
            Team = "Functions"
        }
        row = func.SqlRow(newEmployee)
        employee.set(row);

        return func.HttpResponse(
            body=json.dumps(newEmployee),
            status_code=201,
            mimetype="application/json"
        )
    ```

- Add an import json statement to the top of the file.
- We also need to add the SQL output binding for the `context.bindings.employee` property. Open the function.json file.
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

- Hit 'F5' to run your code. Click the link to upsert the output array values in your SQL table. Your upserted values should launch in the browser.
- Congratulations! You have successfully created your first SQL output binding!

### Samples for Output Bindings

#### Array

See the [AddProductsArray](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-python/AddProductsArray) sample

#### Single Row

See the [AddProduct](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-python/AddProduct) sample

## Python V2 Model

See the Python V2 Model samples [here](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-python-v2/). More information about the Python V2 Model can be found [here](https://learn.microsoft.com/azure/azure-functions/functions-reference-python?tabs=asgi%2Capplication-level&pivots=python-mode-decorators).
