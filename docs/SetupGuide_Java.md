# Azure SQL bindings for Azure Functions - Java

## Table of Contents
- [Azure SQL bindings for Azure Functions - Java](#azure-sql-bindings-for-azure-functions---java)
  - [Table of Contents](#table-of-contents)
  - [Setup Function Project](#setup-function-project)
  - [Input Binding](#input-binding)
    - [SQLInput Attribute](#sqlinput-attribute)
    - [Setup for Input Bindings](#setup-for-input-bindings)
    - [Samples for Input Bindings](#samples-for-input-bindings)
      - [Query String](#query-string)
      - [Empty Parameter Value](#empty-parameter-value)
      - [Null Parameter Value](#null-parameter-value)
      - [Stored Procedure](#stored-procedure)
  - [Output Binding](#output-binding)
    - [SQLOutput Attribute](#sqloutput-attribute)
    - [Setup for Output Bindings](#setup-for-output-bindings)
    - [Samples for Output Bindings](#samples-for-output-bindings)
      - [Array](#array)
      - [Single Row](#single-row)
  - [Known Issues](#known-issues)

## Setup Function Project

These instructions will guide you through creating your Function Project and adding the SQL binding extension. This only needs to be done once for every function project you create. If you have one created already you can skip this step.

1. Install [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local)

2. Create a Function Project for Java:

    ```bash
    mkdir MyApp
    cd MyApp
    func init --worker-runtime java
    ```

3. Enable SQL bindings on the function project. More information can be found in the [Azure SQL bindings for Azure Functions docs](https://aka.ms/sqlbindings).

    Update the `host.json` file to the preview extension bundle.

    ```json
    "extensionBundle": {
        "id": "Microsoft.Azure.Functions.ExtensionBundle.Preview",
        "version": "[4.*, 5.0.0)"
    }
    ```

    Add the Java library for SQL bindings to the pom.xml file.

    ```xml
    <dependency>
        <groupId>com.microsoft.azure.functions</groupId>
        <artifactId>azure-functions-java-library-sql</artifactId>
        <version>[0.1.1,)</version>
    </dependency>
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

- Open your app that you created in [Setup Function Project](#setup-function-project) in VS Code
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
                name = "employees",
                commandText = "SELECT * FROM Employees",
                connectionStringSetting = "SqlConnectionString")
                Employee[] employees) {
        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(employees).build();
    }
    ```

    *In the above, `select * from Employees` is the SQL script run by the input binding. The CommandType on the line below specifies whether the first line is a query or a stored procedure. On the next line, the ConnectionStringSetting specifies that the app setting that contains the SQL connection string used to connect to the database is "SqlConnectionString." For more information on this, see the [SQLInput Attribute](#sqlinput-attribute) section*

- Add `import com.microsoft.azure.functions.sql.annotation.SQLInput;`
- Create a new file and call it `Employee.java`
- Paste the below in the file. These are the column names of our SQL table. Note that the casing of the Object field names and the table column names must match.

    ```java
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

- Open the local.settings.json file, and in the brackets for "Values," verify there is a 'SqlConnectionString.' If not, add it.
- Hit 'F5' to run your code. This will start up the Functions Host with a local HTTP Trigger and SQL Input Binding.
- Click the link that appears in your terminal.
- You should see your database output in the browser window.
- Congratulations! You have successfully created your first SQL input binding!

### Samples for Input Bindings
The database scripts used for the following samples can be found [here](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/Database).

#### Query String

The input binding executes the `SELECT * FROM Products WHERE Cost = @Cost` query, returning the result as Product[], where Product is a user-defined object. The Parameters argument passes the {cost} specified in the URL that triggers the function, getproducts/{cost}, as the value of the @Cost parameter in the query. CommandType is set to `Text`, since the constructor argument of the binding is a raw query.

```java
@FunctionName("GetProducts")
public HttpResponseMessage run(
        @HttpTrigger(
            name = "req",
            methods = {HttpMethod.GET},
            authLevel = AuthorizationLevel.ANONYMOUS,
            route = "getproducts/{cost}")
            HttpRequestMessage<Optional<String>> request,
        @SQLInput(
            name = "products",
            commandText = "SELECT * FROM Products WHERE Cost = @Cost",
            parameters = "@Cost={cost}",
            connectionStringSetting = "SqlConnectionString")
            Product[] products) {

    return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(products).build();
}
```

`Product` is a user-defined object that follows the structure of the Products table. It represents a row of the Products table, with field names and types copying those of the Products table schema. For example, if the Products table has three columns of the form

- **ProductId**: int
- **Name**: varchar
- **Cost**: int

Then the `Product` class would look like

```java
public class Product {
    @JsonProperty("ProductId")
    private int ProductId;
    @JsonProperty("Name")
    private String Name;
    @JsonProperty("Cost")
    private int Cost;

    public Product() {
    }

    public Product(int productId, String name, int cost) {
        ProductId = productId;
        Name = name;
        Cost = cost;
    }

    public int getProductId() {
        return ProductId;
    }

    public void setProductId(int productId) {
        this.ProductId = productId;
    }

    public String getName() {
        return Name;
    }

    public void setName(String name) {
        this.Name = name;
    }

    public int getCost() {
        return Cost;
    }

    public void setCost(int cost) {
        this.Cost = cost;
    }
}
```

#### Empty Parameter Value

In this case, the parameter value of the @Name parameter is an empty string.

```java
@FunctionName("GetProductsNameEmpty")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "getproducts-nameempty/{cost}")
                HttpRequestMessage<Optional<String>> request,
            @SQLInput(
                name = "products",
                commandText = "SELECT * FROM Products WHERE Cost = @Cost and Name = @Name",
                parameters = "@Cost={cost},@Name=",
                connectionStringSetting = "SqlConnectionString")
                Product[] products) {

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(products).build();
    }
```

#### Null Parameter Value

If the `{name}` specified in the `getproducts-namenull/{name}` URL is "null", the query returns all rows for which the Name column is `NULL`. Otherwise, it returns all rows for which the value of the Name column matches the string passed in `{name}`

```java
@FunctionName("GetProductsNameNull")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "getproducts-namenull/{name}")
                HttpRequestMessage<Optional<String>> request,
            @SQLInput(
                name = "products",
                commandText = "IF @Name IS NULL SELECT * FROM Products WHERE Name IS NULL ELSE SELECT * FROM Products WHERE Name = @Name",
                parameters = "@Name={name}",
                connectionStringSetting = "SqlConnectionString")
                Product[] products) {

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(products).build();
    }
```

#### Stored Procedure

`SelectProductsCost` is the name of a procedure stored in the user's database. In this case, *CommandType* is `StoredProcedure`. The parameter value of the `@Cost` parameter in the procedure is once again the `{cost}` specified in the `getproducts-storedprocedure/{cost}` URL.

```java
@FunctionName("GetProductsStoredProcedure")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.GET},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "getproducts-storedprocedure/{cost}")
                HttpRequestMessage<Optional<String>> request,
            @SQLInput(
                name = "products",
                commandText = "SelectProductsCost",
                commandType = "StoredProcedure",
                parameters = "@Cost={cost}",
                connectionStringSetting = "SqlConnectionString")
                Product[] products) {

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(products).build();
    }
```

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

- Open your app in VS Code
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
            name = "output",
            commandText = "dbo.Employees",
            connectionStringSetting = "SqlConnectionString")
            OutputBinding<Employee[]> output) {
        Employee[] employees = new Employee[]
        {
            new Employee(1, "Hello", "World", "Microsoft", "Functions"),
            new Employee(2, "Hi", "SQLupdate", "Microsoft", "Functions")
        };
        output.setValue(employees);
        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(output).build();
    }
    ```

    *In the above, "dbo.Employees" is the name of the table our output binding is upserting into. The line below is similar to the input binding and specifies where our SqlConnectionString is. For more information on this, see the [SQLOutput Attribute](#sqloutput-attribute) section*

- Hit 'F5' to run your code. Click the link to upsert the output array values in your SQL table. Your upserted values should launch in the browser.
- Congratulations! You have successfully created your first SQL output binding!

### Samples for Output Bindings

#### Array

``` java
@FunctionName("AddProductsArray")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.POST},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproducts-array")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                name = "products",
                commandText = "Products",
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<Product[]> products) throws JsonParseException, JsonMappingException, IOException {

        String json = request.getBody().get();
        ObjectMapper mapper = new ObjectMapper();
        Product[] p = mapper.readValue(json, Product[].class);
        products.setValue(p);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(products).build();
    }
```

#### Single Row

```java
    @FunctionName("AddProduct")
    public HttpResponseMessage run(
            @HttpTrigger(
                name = "req",
                methods = {HttpMethod.POST},
                authLevel = AuthorizationLevel.ANONYMOUS,
                route = "addproduct")
                HttpRequestMessage<Optional<String>> request,
            @SQLOutput(
                name = "product",
                commandText = "Products",
                connectionStringSetting = "SqlConnectionString")
                OutputBinding<Product> product) throws JsonParseException, JsonMappingException, IOException {

        String json = request.getBody().get();
        ObjectMapper mapper = new ObjectMapper();
        Product p = mapper.readValue(json, Product.class);
        product.setValue(p);

        return request.createResponseBuilder(HttpStatus.OK).header("Content-Type", "application/json").body(product).build();
    }
```

## Known Issues

- **Planned for Future Support:** The [Azure Functions Java worker](https://github.com/Azure/azure-functions-java-worker) uses the [GSON library](https://github.com/google/gson) to serialize and deserialize data. Since we are unable to customize the GSON serializer in the Java worker, there are limitations with the default GSON serializer settings.
- **Planned for Future Support:** GSON is unable to parse `DATE` and `TIME` values from the SQL table as `java.sql.Date` and `java.sql.Time` types. The current workaround is to use String. Tracking issue: <https://github.com/Azure/azure-functions-sql-extension/issues/422>
- **Planned for Future Support:** On Linux, `java.sql.Timestamp` type gets serialized with an extra comma, causing the upsertion to fail. The current workaround is to use String. Tracking issue: <https://github.com/Azure/azure-functions-sql-extension/issues/521>
