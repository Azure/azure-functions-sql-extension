# Azure SQL bindings for Azure Functions - PowerShell

## Input Binding

### function.json Properties for Input Bindings
```json
{
    "bindings": [
    {
        "authLevel": "anonymous",
        "type": "httpTrigger",
        "direction": "in",
        "name": "Request",
        "methods": [
        "get"
        ]
    },
    {
        "type": "http",
        "direction": "out",
        "name": "Response"
    }
    ]
}
```

### Setup for Input Bindings

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./QuickStart.md#create-a-sql-server).

- Open your app that you created in [Create a Function App](./QuickStart.md#create-a-function-app) in VS Code
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger -> (Provide a function name) -> anonymous
- In the file that opens (`run.ps1`), replace the code within the file the below code.

    ```powershell
    using namespace System.Net

    param($Request, $employee)
    
    Write-Host "PowerShell function with SQL Input Binding processed a request."
    
    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [System.Net.HttpStatusCode]::OK
        Body = $employee
    })
    ```

- We also need to add the SQL input binding for the `employee` parameter. Open the function.json file.
- Paste the below in the file as an additional entry to the "bindings": [] array.

    ```json
    {
      "name": "employee",
      "type": "sql",
      "direction": "in",
      "commandText": "select * from Employees",
      "commandType": "Text",
      "connectionStringSetting": "SqlConnectionString"
    }
    ```

    *In the above, "select * from Employees" is the SQL script run by the input binding. The CommandType on the line below specifies whether the first line is a query or a stored procedure. On the next line, the ConnectionStringSetting specifies that the app setting that contains the SQL connection string used to connect to the database is "SqlConnectionString." For more information on this, see the [function.json Properties for Input Bindings](#functionjson-properties-for-input-bindings) section*

- Open the local.settings.json file, and in the brackets for "Values," verify there is a 'SqlConnectionString.' If not, add it.
- Hit 'F5' to run your code. This will start up the Functions Host with a local HTTP Trigger and SQL Input Binding.
- Click the link that appears in your terminal.
- You should see your database output in the browser window.
- Congratulations! You have successfully created your first SQL input binding!

### Samples for Input Bindings

#### Query String

_TODO_

#### Empty Parameter Value

_TODO_

#### Null Parameter Value

_TODO_

#### Stored Procedure

_TODO_

## Output Binding

### function.json Properties for Output Bindings

```json
{
    "authLevel": "anonymous",
    "type": "httpTrigger",
    "direction": "in",
    "name": "Request",
    "methods": [
        "post"
    ]
},
{
    "type": "http",
    "direction": "out",
    "name": "Response"
},
```

### Setup for Output Bindings

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./QuickStart.md#create-a-sql-server).

- Open your app in VS Code
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger ->  (Provide a function name) -> anonymous
- In the file that opens (`run.ps1`), replace the code within the file the below code.

   ```powershell
    using namespace System.Net

    param($Request)

    Write-Host "PowerShell function with SQL Output Binding processed a request."

    # Update req_body with the body of the request
    $req_body = @(
        @{
            EmployeeId=1,
            FirstName="Hello",
            LastName="World",
            Company="Microsoft",
            Team="Functions"
        },
        @{
            EmployeeId=2,
            FirstName="Hi",
            LastName="SQLupdate",
            Company="Microsoft",
            Team="Functions"
        }
    );
    # Assign the value we want to pass to the SQL Output binding. 
    # The -Name value corresponds to the name property in the function.json for the binding
    Push-OutputBinding -Name employee -Value $req_body

    Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::OK
        Body = $req_body
    })
    ```

- We also need to add the SQL output binding for the `employee` parameter. Open the function.json file.
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

#### ICollector&lt;T&gt;/IAsyncCollector&lt;T&gt;

_TODO_

#### Array

_TODO_

#### Single Row

_TODO_

### Primary Key Special Cases

Typically Output Bindings require two things:

1. The table being upserted to contains a Primary Key constraint (composed of one or more columns)
2. Each of those columns must be present in the POCO object used in the attribute

Normally either of these are false then an error will be thrown. Below are the situations in which this is not the case:

#### Identity Columns
In the case where one of the primary key columns is an identity column, there are two options based on how the function defines the output object:

1. If the identity column isn't included in the output object then a straight insert is always performed with the other column values. See [AddProductWithIdentityColumn](../samples/samples-powershell/AddProductWithIdentityColumn/run.ps1) for an example.
2. If the identity column is included (even if it's an optional nullable value) then a merge is performed similar to what happens when no identity column is present. This merge will either insert a new row or update an existing row based on the existence of a row that matches the primary keys (including the identity column). See [AddProductWithIdentityColumnIncluded](../samples/samples-powershell/AddProductWithIdentityColumnIncluded/run.ps1) for an example.

#### Columns with Default Values
In the case where one of the primary key columns has a default value, there are also two options based on how the function defines the output object:
1. If the column with a default value is not included in the output object, then a straight insert is always performed with the other values. See [AddProductWithDefaultPK](../samples/samples-powershell/AddProductWithDefaultPK/run.ps1) for an example.
2. If the column with a default value is included then a merge is performed similar to what happens when no default column is present. If there is a nullable column with a default value, then the provided column value in the output object will be upserted even if it is null.

## Trigger Binding

> Trigger binding support is only available for C# functions at present.
