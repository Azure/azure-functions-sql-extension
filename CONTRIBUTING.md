# Contributing

This project welcomes contributions and suggestions. Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the Microsoft Open Source Code of Conduct. For more information see the Code of Conduct FAQ or contact opencode@microsoft.com with any additional questions or comments.

<br>

## Contributer Getting Started

### SQL Setup

This requires already having a SQL database. If you need to create a SQL database, we recommend one of 2 options:
- [Local SQL Server running in a Docker container](https://docs.microsoft.com/sql/linux/quickstart-install-connect-docker)
- [Azure SQL Database](#Create-Azure-SQL-Database).

A primary key must be set in your SQL table before using the bindings. To do this, run the below SQL commands in the SQL query editor. Note that this step needs to only be done once. If this has already been done, you can safely proceed to [Set Up Development Environment](#set-up-development-environment).

1. Ensure there are no NULL values in the primary key column. The primary key will usually be an ID column.

    ```sql
    ALTER TABLE [TableName] ALTER COLUMN [PrimaryKeyColumnName] int NOT NULL
    ```

2. Set primary key column.

    ```sql
    ALTER TABLE [TableName] ADD CONSTRAINT PKey PRIMARY KEY CLUSTERED ([PrimaryKeyColumn]);
    ```

3. Congrats on setting up your database! Now continue to set up your local environment and complete the quick start.

### Set Up Development Environment

1. [Install VS Code](https://code.visualstudio.com/Download)
   
2. Clone repo and open in VS Code:

```bash
git clone https://github.com/Azure/azure-functions-sql-extension
cd azure-functions-sql-extension
code .
```
3. Install extensions when prompted after VS Code opens
   - Note: This includes the Azure Functions, C#, and editorconfig extensions

4. Get your SqlConnectionString. 
   
    If you provisioned an Azure SQL Database, your connection string can be found in your SQL database resource by going to the left blade and clicking 'Connection strings'. Copy the Connection String.

    - (*Note: when pasting in the connection string, you will need to replace part of the connection string where it says '{your_password}' with your Azure SQL Server password*)

    If your database wasn't provisioned in Azure, please follow documentation [here](https://docs.microsoft.com/sql/connect/homepage-sql-connection-programming), if you're unfamliar with how to construct a connection string.

5. In 'local.settings.json' in 'Values', verify you have the below. If not, add the below and replace "Your Connection String" with the your connection string from the previous step:

    ```json
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "AzureWebJobsDashboard": "UseDevelopmentStorage=true",
    "SqlConnectionString": "<Your Connection String>"
    ```
6. Press F5 to run SQL bindings samples that are included in this repo.