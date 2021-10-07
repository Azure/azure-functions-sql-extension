# Running Tests

## Running Integration Tests
Our integration tests are based on functions from the samples project. To run integration tests, you will need
1. [Azure Functions Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools) - This is used to start the functions runtime.

   Installation with npm:
   ```
   npm install -g azure-functions-core-tools
   ```
2. [Azurite Emulator for Local Azure Storage](https://docs.microsoft.com/azure/storage/common/storage-use-azurite?tabs=npm#install-and-run-azurite) - This is required to run non-HTTP binding functions.

   Installation with npm:
   ```
   npm install -g azurite
   ```
3. A local SQL Server instance - This is used by tests to verify that data is correctly added/fetched from the database when a test Function is run. You just need the server to be up and running, the tests will create the database and tables which will be cleaned up afterwards.

   - You can either have a SQL Server installation with `localhost` available for connection via integrated security, or
   - Start a SQL Server instance with Docker
     ```
     docker pull mcr.microsoft.com/mssql/server:2019-latest
     docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD={your_password}" -e "MSSQL_PID=Express" -p 1433:1433 --name sql1 -h sql1 -d mcr.microsoft.com/mssql/server:2019-latest
     ```
     After the Docker image is running, you just need to set `SA_PASSWORD` environment variable to `{your_password}` and can run tests normally.
     
     Note: If `SA_PASSWORD` is not set, the tests will assume you're using a local MSSQL installation and default to using integrated auth. MSSQL on Docker does not support integrated auth by default.