# SQL binding extension for Azure Functions

Azure SQL bindings for Azure Functions adds input and output bindings for Azure SQL and SQL Server products.
- **Input Binding**: takes a SQL query to run and returns the output of the query in the function.
- **Output Binding**: takes a list of rows and upserts them into the user table (i.e. If a row doesn't already exist, it is added. If it does, it is updated).

Get started quickly with the samples available in our [repository](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples)

Further information on the Azure SQL binding for Azure Functions is also available in the [Azure Functions docs](https://docs.microsoft.com/azure/azure-functions/functions-bindings-azure-sql).

Find latest updates at [Release notes](https://github.com/Azure/azure-functions-sql-extension/releases/latest)