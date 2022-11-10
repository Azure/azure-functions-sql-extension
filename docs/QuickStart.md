# Quick Start

This document contains all the initial setup instructions needed to make your own Azure Function with SQL Bindings.

## Create a SQL Server

First you'll need a SQL server for the bindings to connect to. If you already have your own set up then you can skip this step, otherwise pick from one of the below options.

### Docker container

SQL Server on Docker makes it easy to set up and connect to a locally hosted instance of SQL Server. Instructions for getting started can be found [here](https://docs.microsoft.com/sql/linux/sql-server-linux-docker-container-deployment).

### Azure SQL Database

Azure SQL Database is a fully managed platform as a service (PaaS) database engine that runs the latest stable version of the Microsoft SQL Server database engine. Instructions for getting started can be found [here](https://docs.microsoft.com/azure/azure-sql/database/single-database-create-quickstart).


## SQL Setup

Next you'll configure your SQL Server database for use with Azure SQL binding for Azure Functions.

This will require connecting to and running queries - you can use [Azure Data Studio](https://docs.microsoft.com/sql/azure-data-studio/download-azure-data-studio) or the [MSSQL for VS Code Extension](https://docs.microsoft.com/sql/tools/visual-studio-code/sql-server-develop-use-vscode) to do this.

1. First you'll need a table to run queries against. If you already have one you'd like to use then you can skip this step.

    Otherwise connect to your database and run the following query to create a simple table to start with.

```sql
CREATE TABLE Employees (
        EmployeeId int,
        FirstName varchar(255),
        LastName varchar(255),
        Company varchar(255),
        Team varchar(255)
);
```

2. Next a primary key must be set in your SQL table before using the bindings. To do this, run the queries below, replacing the placeholder values for your table and column.

```sql
ALTER TABLE ['{table_name}'] ALTER COLUMN ['{primary_key_column_name}'] int NOT NULL

ALTER TABLE ['{table_name}'] ADD CONSTRAINT PKey PRIMARY KEY CLUSTERED (['{primary_key_column_name}']);
```


## Create a Function App

Now you will need a Function App to add the binding to. If you have one created already you can skip this step.

These steps can be done in the Terminal/CLI or with PowerShell.

1. Install [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local)

2. Create a function app for .NET, JavaScript, TypeScript, Python or Java.

    **.NET**
    ```bash
    mkdir MyApp
    cd MyApp
    func init --worker-runtime dotnet
    ```

    **JavaScript (NodeJS)**
    ```bash
    mkdir MyApp
    cd MyApp
    func init --worker-runtime node --language javascript
    ```

    **TypeScript (NodeJS)**
    ```bash
    mkdir MyApp
    cd MyApp
    func init --worker-runtime node --language typescript
    ```

    **Python**

    *See [#250](https://github.com/Azure/azure-functions-sql-extension/issues/250) before starting.*
    ```bash
    mkdir MyApp
    cd MyApp
    func init --worker-runtime python
    ```

    **Java**
    ```bash
    mkdir MyApp
    cd MyApp
    func init --worker-runtime java
    ```

    **PowerShell**
    ```bash
    mkdir MyApp
    cd MyApp
    func init --worker-runtime powershell
    ```

3. Enable SQL bindings on the function app. More information can be found [in Microsoft Docs](https://docs.microsoft.com/azure/azure-functions/functions-bindings-azure-sql).

    **.NET:** Install the extension.

    ```powershell
    dotnet add package Microsoft.Azure.WebJobs.Extensions.Sql --prerelease
    ```

    **JavaScript and TypeScript:** Update the `host.json` file to the preview extension bundle.
    ```json
    "extensionBundle": {
        "id": "Microsoft.Azure.Functions.ExtensionBundle.Preview",
        "version": "[4.*, 5.0.0)"
    }
    ```

    **Python:**

    Update the `host.json` file to the preview extension bundle.
    ```json
    "extensionBundle": {
        "id": "Microsoft.Azure.Functions.ExtensionBundle.Preview",
        "version": "[4.*, 5.0.0)"
    }
    ```

    Add a preview version of the Python functions library to `requirements.txt`.
    ```txt
    azure-functions==1.11.3b1
    ```

    Add a setting in `local.settings.json` to isolate the worker dependencies.
    ```json
    "PYTHON_ISOLATE_WORKER_DEPENDENCIES": "1"
    ```

    **Java:**
    Update the `host.json` file to the preview extension bundle.
    ```json
    "extensionBundle": {
        "id": "Microsoft.Azure.Functions.ExtensionBundle.Preview",
        "version": "[4.*, 5.0.0)"
    }
    ```

    Add the `azure-functions-java-library-sql` dependency to the pom.xml file.
    ```xml
    <dependency>
        <groupId>com.microsoft.azure.functions</groupId>
        <artifactId>azure-functions-java-library-sql</artifactId>
        <version>0.1.0</version>
    </dependency>
    ```

     **PowerShell:**
    Update the `host.json` file to the preview extension bundle.
    ```json
    "extensionBundle": {
        "id": "Microsoft.Azure.Functions.ExtensionBundle.Preview",
        "version": "[4.*, 5.0.0)"
    }
    ```

## Configure Function App

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
   - Browse to the SQL Database resource in the <a href="https://ms.portal.azure.com/">Azure portal</a></br>
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

2. You have setup your local environment and are now ready to create your first Azure Function with SQL bindings! Continue to the language specific guides for the next steps in creating and configuration your function!

- [.NET](./BindingsGuide_Dotnet.md)
- [Java](./BindingsGuide_Java.md)
- [Javascript](./BindingsGuide_Javascript.md)
- [Python](./BindingsGuide_Python.md)
- [PowerShell](./BindingsGuide_PowerShell.md)