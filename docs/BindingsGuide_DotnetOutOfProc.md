# SQL Extension .NET Worker

Welcome to the Sql Extension .NET Worker Repository. The .NET Worker provides .NET 6 support for SQL Bindings in Azure Functions, introducing an **Isolated Model**, running as an out-of-process language worker that is separate from the Azure Functions runtime. This allows you to have full control over your application's dependencies as well as other new features like a middleware pipeline. A .NET Isolated function app works differently than a .NET Core 3.1 function app. For .NET Isolated, you build an executable that imports the .NET Isolated language worker as a NuGet package. Your app includes a [`Program.cs`](https://github.com/Azure/azure-functions-sql-extension/blob/main/samples/samples-outofproc/Program.cs) that starts the worker.

## Binding Model

.NET Isolated introduces a new binding model, slightly different from the binding model exposed in .NET Core 3 Azure Functions. More information can be [found here](https://github.com/Azure/azure-functions-dotnet-worker/wiki/.NET-Worker-bindings). Please review our samples for usage information.

## Samples

You can find samples on how to use Sql Extension .NET Worker under `samples\samples-outofproc` ([link](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-outofproc)).

## Create and run .NET Isolated functions

**Note: Visual Studio and Visual Studio Code support is on the way. In the meantime, please use `azure-functions-core-tools` or the sample projects as a starting point.**  

### Install .NET 6.0

Download .NET 6.0 [from here](https://dotnet.microsoft.com/download/dotnet/6.0)

### Install the Azure Functions Core Tools

To download Core Tools, please check out our docs at [Azure Functions Core Tools](https://github.com/Azure/azure-functions-core-tools)

### Create an Isolated Function App

Run the below in the Terminal/CLI or with PowerShell.

    ```
    mkdir MyApp
    cd MyApp
    func init --worker-runtime dotnet (Isolated Process)
    ```

   or in an empty directory, run `func init` and select `dotnet (Isolated Process)`

### Add a function: 
Run `func new` and select `HttpTrigger` trigger. Fill in the function name.

### Configure Function App

1. Get your SQL connection string
    
    Local SQL Server - Use this connection string, replacing the placeholder values for the database and password.  
      
    `Server=localhost;Initial Catalog={db_name};Persist Security Info=False;User ID=sa;Password={your_password};` Azure SQL Server - Browse to the SQL Database resource in the [Azure portal](https://ms.portal.azure.com/)  
    \- In the left blade click on the **Connection Strings** tab  
    \- Copy the **SQL Authentication** connection string  
      
    (_Note: when pasting in the connection string, you will need to replace part of the connection string where it says '{your\_password}' with your Azure SQL Server password_)
2. Open the generated `local.settings.json` file and in the `Values` section verify you have the below. If not, add the below and replace `{connection_string}` with the your connection string from the previous step:
    
    ```
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "SqlConnectionString": "{connection_string}"
    
    ```
    
3. Verify your `host.json` looks like the below:
    
    ```
    {
        "version": "2.0",
        "logging": {
            "applicationInsights": {
                "samplingSettings": {
                    "isEnabled": true,
                    "excludedTypes": "Request"
                }
            }
        }
    }
    
    ```
    
4. You have setup your local environment and are now ready to create your first out of proc SQL bindings! Continue to the [input](#Input-Binding-Tutorial) and [output](#Output-Binding-Tutorial) binding tutorials, or refer to [Samples](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-outofproc) for information on how to use the bindings out of proc and explore on your own.
    

### Run functions locally

Run `func host start` in the sample function app directory.

## Tutorials

#### Input Binding Tutorial

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](https://github.com/Azure/azure-functions-sql-extension/blob/main/README.md#Create-a-SQL-Server).

- Open your app that you created in [Create a Function App](#create-an-isolated-function-app) in VS Code
- Press 'F1' and search for 'Azure Functions: Create Function'
- Choose HttpTrigger -> (Provide a function name) -> Company.namespace -> anonymous
- In the file that opens, replace the `public static async Task<IActionResult> Run` block with the below code.

    ```csharp
    [Function("GetEmployees")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "employees")] HttpRequest req,
        ILogger log,
        [SqlInput("SELECT * FROM Employees",
        CommandType = System.Data.CommandType.Text,
        ConnectionStringSetting = "SqlConnectionString")]
        IEnumerable<Employee> employees)
    {
        return new OkObjectResult(employees);
    }
    ```

    *In the above, "select * from Employees" is the SQL script run by the input binding. The CommandType on the line below specifies whether the first line is a query or a stored procedure. On the next line, the ConnectionStringSetting specifies that the app setting that contains the SQL connection string used to connect to the database is "SqlConnectionString." For more information on this, see the [Input Binding](https://github.com/Azure/azure-functions-sql-extension/blob/main/README.md#Input-Binding) section*

- Add 'using Microsoft.Azure.Functions.Worker.Extension.Sql;' for using *SqlInput*, the out of proc sql input binding.
- Add 'using System.Collections.Generic;' to the namespaces list at the top of the page.
- Currently, there is an error for the IEnumerable. We'll fix this by creating an Employee class.
- Create a new file and call it 'Employee.cs'
- Paste the below in the file. These are the column names of our SQL table.

    ```csharp
    namespace Company.Function {
        public class Employee{
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
- Congratulations! You have successfully created your first out of proc SQL input binding! Checkout [Input Binding](https://github.com/Azure/azure-functions-sql-extension/blob/main/README.md#Input-Binding) for more information on how to use it and explore on your own!

#### Output Binding Tutorial

Note: This tutorial requires that a SQL database is setup as shown in [Create a SQL Server](https://github.com/Azure/azure-functions-sql-extension/blob/main/README.md#Create-a-SQL-Server), and that you have the 'Employee.cs' class from the [Input Binding Tutorial](#Input-Binding-Tutorial).

- Open your app in VSCode
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

    *In the above, "dbo.Employees" is the name of the table our output binding is upserting into. The line below is similar to the input binding and specifies where our SqlConnectionString is. For more information on this, see the [Output Binding](https://github.com/Azure/azure-functions-sql-extension/blob/main/README.md#Output-Binding) section*

- Add 'using Microsoft.Azure.Functions.Worker.Extension.Sql;' for using *SqlOutput*, the out of proc sql output binding.
- Hit 'F5' to run your code. Click the link to upsert the output array values in your SQL table. Your upserted values should launch in the browser.
- Congratulations! You have successfully created your first out of proc SQL output binding! Checkout [Output Binding](https://github.com/Azure/azure-functions-sql-extension/blob/main/README.md#Output-Binding) for more information on how to use it and explore on your own!

## Differences from in-process bindings
As stated in the functions [documentation](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide#output-bindings)
- Because .NET isolated projects run in a separate worker process, bindings can't take advantage of rich binding classes, such as ICollector<T>, IAsyncCollector<T>, and CloudBlockBlob.
- There's also no direct support for types inherited from underlying service SDKs, such as SqlCommand. Instead, bindings rely on strings, arrays, and serializable types, such as plain old class objects (POCOs).
- For HTTP triggers, you must use HttpRequestData and HttpResponseData to access the request and response data. This is because you don't have access to the original HTTP request and response objects when running out-of-process.

