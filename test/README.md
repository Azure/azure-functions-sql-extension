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
3. A local SQL Server instance or an Azure SQL Database - This is used by tests to verify that data is correctly added/fetched from the database when a test Function is run. You just need the server to be up and running, the tests will create the database and tables which will be cleaned up afterwards.

   - You can either have a SQL Server installation with `localhost` available for connection via integrated security, or
   - Start a SQL Server instance with Docker
     ```
     docker pull mcr.microsoft.com/mssql/server:2019-latest
     docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD={your_password}" -e "MSSQL_PID=Express" -p 1433:1433 --name sql1 -h sql1 -d mcr.microsoft.com/mssql/server:2019-latest
     ```
     After the Docker image is running, you just need to set `SA_PASSWORD` environment variable to `{your_password}` and can run tests normally.
     
     Note: If `SA_PASSWORD` is not set, the tests will assume you're using a local MSSQL installation and default to using integrated auth. MSSQL on Docker does not support integrated auth by default.
   - To use an Azure SQL Database, set the `AZURE_SQL_DB_CONNECTION_STRING` environment variable to your Azure SQL Datbase connection string.

 ## Adding New Integration Tests
   When adding a new integration test for a function follow these steps:

   1. First decide where the function being used for the test is going to go. If this is demonstrating valid functionality that customers may find useful then it should go under the samples folder. If it is demonstrating an error case or something similar then it should go under the test/Integration folder
   2. Within either of those folders are a number of sub-folders with the name samples-\<language> or test-\<language>. You will need to make a version of the function for each of the currently supported languages - skipping any that don't apply to your sample (for example if you're verifying a language feature that only exists in one particular language. These functions should all be functionally identical - given the same input they should return the same output
   3. The function should have the FunctionName be the same as the class name
   4. After the functions are created then add the test itself to either SqlInputBindingIntegrationTests.ts or SqlOutputBindingIntegrationTests.ts. See below for the various attributes, parameters and setup that are required for each test

   ### SqlInlineData attribute:
     
   SqlInlineData attribute is a custom attribute derived from Xunit Data attribute and it supplies the SupportedLanguage parameter to the test for the test to run against in addition to any other data parameters included. By default any test decorated with the [SqlInlineData] attribute will be run against each supported language in the SupportedLanguages enum.

   How to use: Add [Theory] and [SqlInlineData] attributes over the test and pass in the test variables except the language variable.
     [SqlInlineData] runs the test with the given parameters against all supported languages.

   Ex: When the test doesn't have any input parameters:
   ```
        [Theory]
        [SqlInlineData()]
        public async void GetProductsByCostTest(SupportedLanguages lang)
        {
               this.StartFunctionHost(nameof(<FUNCTIONNAME>), lang); // Replace <FUNCTIONNAME> with the class name of the function this test is running against
            // test code here
        }
   ```  
   Ex: When the test has parameters:

   ```
        [Theory]
        [SqlInlineData(0, 0)]
        public async void GetProductsByCostTest(int n, int cost, SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(<FUNCTIONNAME>), lang); // Replace <FUNCTIONNAME> with the class name of the function this test is running against
            // test code here
        }
   ```

   ### UnsupportedLanguages attribute:

   UnsupportedLanguages attribute accepts a list of comma separated languages that are not supported for the test. For example, if a test is not relevant to a specific language or if the function is not yet available in the samples-\<language>, we should use UnsupportedLanguages attribute and specify the language(s) for exclusion.

   Below is an example on how to use the attribute.

   Use [UnsupportedLanguages] attribute over the test to specify the list of languages that the test must not run against  and add a comment on why the language is not supported.

   ```
        [Theory]
        [SqlInlineData()]
        [UnsupportedLanguages(SupportedLanguages.JavaScript)] // Collectors are only available in C#
        public void AddProductsCollectorTest(SupportedLanguages lang)
        {
            this.StartFunctionHost(nameof(<FUNCTIONNAME>), lang); // Replace <FUNCTIONNAME> with the class name of the function this test is running against
            // test code here
        }
   ```