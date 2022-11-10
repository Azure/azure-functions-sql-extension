# Azure SQL bindings for Azure Functions - Java

## Input Binding

### SQLInput Attribute

_TODO_

### Setup for Input Bindings

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./QuickStart.md#create-a-sql-server).

- Open your app that you created in [Create a Function App](./QuickStart.md#create-a-function-app) in VS Code
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

    *In the above, "select * from Employees" is the SQL script run by the input binding. The CommandType on the line below specifies whether the first line is a query or a stored procedure. On the next line, the ConnectionStringSetting specifies that the app setting that contains the SQL connection string used to connect to the database is "SqlConnectionString." For more information on this, see the [SqlAttribute for Input Bindings](#sqlattribute-for-input-bindings) section*

- Add 'import com.microsoft.azure.functions.sql.annotation.SQLInput;'
- Create a new file and call it 'Employee.java'
- Paste the below in the file. These are the column names of our SQL table.

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

### SQLOutput Attribute

_TODO_

### Setup for Output Bindings

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](./QuickStart.md#create-a-sql-server), and that you have the 'Employee.java' class from the [Setup for Input Bindings](#setup-for-input-bindings) section.

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

    *In the above, "dbo.Employees" is the name of the table our output binding is upserting into. The line below is similar to the input binding and specifies where our SqlConnectionString is. For more information on this, see the [SqlAttribute for Output Bindings](#sqlattribute-for-output-bindings) section*

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

Typically Output Bindings require two things :

1. The table being upserted to contains a Primary Key constraint (composed of one or more columns)
2. Each of those columns must be present in the POCO object used in the attribute

Normally either of these are false then an error will be thrown. Below are the situations in which this is not the case :

#### Identity Columns
In the case where one of the primary key columns is an identity column, there are two options based on how the function defines the output object:

1. If the identity column isn't included in the output object then a straight insert is always performed with the other column values. See [AddProductWithIdentityColumn](../samples/samples-java/src/main/java/com/function/AddProductWithIdentityColumn.java) for an example.
2. If the identity column is included (even if it's an optional nullable value) then a merge is performed similar to what happens when no identity column is present. This merge will either insert a new row or update an existing row based on the existence of a row that matches the primary keys (including the identity column). See [AddProductWithIdentityColumnIncluded](../samples/samples-java/src/main/java/com/function/AddProductWithIdentityColumnIncluded.java) for an example.

#### Columns with Default Values
In the case where one of the primary key columns has a default value, there are also two options based on how the function defines the output object:
1. If the column with a default value is not included in the output object, then a straight insert is always performed with the other values. See [AddProductWithDefaultPK](../samples/samples-java/src/main/java/com/function/AddProductWithDefaultPK.java) for an example.
2. If the column with a default value is included then a merge is performed similar to what happens when no default column is present. If there is a nullable column with a default value, then the provided column value in the output object will be upserted even if it is null.

## Trigger Binding

> Trigger binding support is only available for C# functions at present.