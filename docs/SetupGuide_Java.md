# Azure SQL bindings for Azure Functions - Java

## Table of Contents
- [Azure SQL bindings for Azure Functions - Java](#azure-sql-bindings-for-azure-functions---java)
  - [Table of Contents](#table-of-contents)
  - [Setup Function App](#setup-function-app)
  - [Input Binding](#input-binding)
    - [SQLInput Attribute](#sqlinput-attribute)
    - [Setup for Input Bindings](#setup-for-input-bindings)
    - [Samples for Input Bindings](#samples-for-input-bindings)
      - [Query String](#query-string)
      - [Empty Parameter Value](#empty-parameter-value)
      - [Null Parameter Value](#null-parameter-value)
      - [Stored Procedure](#stored-procedure)
      - [IAsyncEnumerable](#iasyncenumerable)
  - [Output Binding](#output-binding)
    - [SQLOutput Attribute](#sqloutput-attribute)
    - [Setup for Output Bindings](#setup-for-output-bindings)
    - [Samples for Output Bindings](#samples-for-output-bindings)
      - [ICollector\<T\>/IAsyncCollector\<T\>](#icollectortiasynccollectort)
      - [Array](#array)
      - [Single Row](#single-row)
  - [Trigger Binding](#trigger-binding)

## Setup Function App

These instructions will guide you through creating your Function App and adding the SQL binding extension. This only needs to be done once for every function app you create. If you have one created already you can skip this step.

1. Install [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local)

2. Create a function app for Java:
    ```bash
    mkdir MyApp
    cd MyApp
    func init --worker-runtime java
    ```

3. Enable SQL bindings on the function app. More information can be found in the [Azure SQL bindings for Azure Functions docs](https://aka.ms/sqlbindings).

    Update the `host.json` file to the preview extension bundle.
    ```json
    "extensionBundle": {
        "id": "Microsoft.Azure.Functions.ExtensionBundle.Preview",
        "version": "[4.*, 5.0.0)"
    }
    ```

## Input Binding

See [Input Binding Overview](./BindingsOverview.md#input-binding) for general information about the Azure SQL Input binding.

### SQLInput Attribute

In the Java functions runtime library, use the @SQLInput annotation (com.microsoft.azure.functions.sql.annotation.SQLInput) on parameters whose value comes from the query specified by commandText. This annotation supports the following elements:

| Element |Description|
|---------|---------|
|**name** |  Required. The variable name used in function.json. |
| **commandText** | Required. The Transact-SQL query command or name of the stored procedure executed by the binding.  |
| **connectionStringSetting** | Required. The name of an app setting that contains the connection string for the database against which the query or stored procedure is being executed. This value isn't the actual connection string and must instead resolve to an environment variable name.  Optional keywords in the connection string value are [available to refine SQL bindings connectivity](https://aka.ms/sqlbindings#sql-connection-string). |
| **commandType** | A [CommandType](https://learn.microsoft.com/dotnet/api/system.data.commandtype) value, which is [Text](https://learn.microsoft.com/dotnet/api/system.data.commandtype#fields) for a query and [StoredProcedure](https://learn.microsoft.com/dotnet/api/system.data.commandtype#fields) for a stored procedure. |
| **parameters** | Zero or more parameter values passed to the command during execution as a single string. Must follow the format `@param1=param1,@param2=param2`. Neither the parameter name nor the parameter value can contain a comma (`,`) or an equals sign (`=`). |

When you're developing locally, add your application settings in the local.settings.json file in the Values collection.

### Setup for Input Bindings

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./GeneralSetup.md#create-a-sql-server).

- Open your app that you created in [Create a Function App](./GeneralSetup.md#create-a-function-app) in VS Code
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger -> (Provide a package name) -> (Provide a function name) -> anonymous
- In the file that opens, replace the `public HttpResponseMessage run` block with the below code.

    ```java
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET, HttpMethod.POST},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "getemployees")
                HttpRequestMessage<Optional<String>> request,
            @SQLInput(
                commandText = "SELECT * FROM Employees",
                commandType = "Text",
                connectionStringSetting = "SqlConnectionString")
                Employee[] employees) {
        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(employees).build();
    }
    ```

    *In the above, "select * from Employees" is the SQL script run by the input binding. The CommandType on the line below specifies whether the first line is a query or a stored procedure. On the next line, the ConnectionStringSetting specifies that the app setting that contains the SQL connection string used to connect to the database is "SqlConnectionString." For more information on this, see the [SQLInput Attribute](#sqlinput-attribute) section*

- Add 'import com.microsoft.azure.functions.sql.annotation.SQLInput;'
- Create a new file and call it 'Employee.java'
- Paste the below in the file. These are the column names of our SQL table. Note that the casing of the Object field names and the table column names must match.

    ```java
    package com.function.Common;
    public class Employee {
        private int EmployeeId;
        private String LastName;
        private String FirstName;
        private String Company;
        private String Team;
        public Employee() {
        }
        public Employee(int employeeId, String lastName, String firstName, String company, String team) {
            EmployeeId = employeeId;
            LastName = lastName;
            FirstName = firstName;
            Company = company;
            Team = team;
        }
        public int getEmployeeId() {
            return EmployeeId;
        }
        public void setEmployeeId(int employeeId) {
            this.EmployeeId = employeeId;
        }
        public String getLastName() {
            return LastName;
        }
        public void setLastName(String lastName) {
            this.LastName = lastName;
        }
        public String getFirstName() {
            return FirstName;
        }
        public void setFirstName(String firstName) {
            this.FirstName = firstName;
        }
        public String getCompany() {
            return Company;
        }
        public void setCompany(String company) {
            this.Company = company;
        }
        public String getTeam() {
            return Team;
        }
        public void setTeam(String team) {
            this.Team = team;
        }
    }
    ```

- Navigate back to your HttpTriggerJava file.
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

#### IAsyncEnumerable

_TODO_

## Output Binding

See [Output Binding Overview](./BindingsOverview.md#output-binding) for general information about the Azure SQL Output binding.

### SQLOutput Attribute

In the Java functions runtime library, use the @SQLOutput annotation (com.microsoft.azure.functions.sql.annotation.SQLOutput) on parameters whose values you want to upsert into the target table. This annotation supports the following elements:

| Element |Description|
|---------|---------|
|**name** |  Required. The variable name used in function.json. |
| **commandText** | Required. The name of the table being written to by the binding.  |
| **connectionStringSetting** | Required. The name of an app setting that contains the connection string for the database to which data is being written. This isn't the actual connection string and must instead resolve to an environment variable. Optional keywords in the connection string value are [available to refine SQL bindings connectivity](https://aka.ms/sqlbindings#sql-connection-string). |

When you're developing locally, add your application settings in the local.settings.json file in the Values collection.

### Setup for Output Bindings

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./GeneralSetup.md#create-a-sql-server), and that you have the 'Employee.java' class from the [Setup for Input Bindings](#setup-for-input-bindings) section.

- Open your app in VSCode
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger -> (Provide a package name) -> (Provide a function name) -> anonymous
- In the file that opens, replace the `public HttpResponseMessage run` block with the below code.

    ```java
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addemployees-array")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                commandText = "dbo.Employees",
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<Employee[]> output) {
        output = new Employee[]
            {
                new Employee(1, "Hello", "World", "Microsoft", "Functions"),
                new Employee(2, "Hi", "SQLupdate", "Microsoft", "Functions")
            };
        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(output).build();
    }
    ```

    *In the above, "dbo.Employees" is the name of the table our output binding is upserting into. The line below is similar to the input binding and specifies where our SqlConnectionString is. For more information on this, see the [SQLOutput Attribute](#sqloutput-attribute) section*

- Hit 'F5' to run your code. Click the link to upsert the output array values in your SQL table. Your upserted values should launch in the browser.
- Congratulations! You have successfully created your first SQL output binding!

### Samples for Output Bindings

#### ICollector&lt;T&gt;/IAsyncCollector&lt;T&gt;

_TODO_

#### Array

_TODO_

#### Single Row

_TODO_

## Trigger Binding

> Trigger binding support is only available for in-proc C# functions at present.