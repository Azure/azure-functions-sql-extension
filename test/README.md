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

 ## Adding New Integration Tests

    When addiing a new integration test for a function, make sure to add the function in all supported languages (Under samples\/samples-\<language\> in samples project) unless the test is specific to some language(s). All Integration tests must have atleast a SupportedLanguage input parameter that specifies the language the test runs against.

     ### SqlInlineData attribute:
     
     SqlInlineData attribute is a custom attribute derived from Xunit Data attribute and it supplies the SupportedLanguage parameter to the test for the test to run against. By default any test decorated with the [SqlInline] attribute will be run against each supported language in the SupportedLanguages enum.

      ### UnsupportedLanguages attribute:
      UnsupportedLanguages attribute accepts a list of coma separated languages that are not supported for the test. For example, if a test is not relevant to a specific language or if the function is not yet available in the samples-\<language>, we should use UnsupportedLanguages attribute and specify the language(s) for exclusion.

      Below are some examples on how to use the attributes.

    1. Use [Theory] and [SqlInlineData] attributes over the test and pass in the test variables except the language variable.
     [SqlInlineData] runs the test with the given parameters against all supported languages.
     Ex: When the test doesn't have any input paraneters:
     ```
        [Theory]
        [SqlInlineData()]
        public async void GetProductsByCostTest(SupportedLanguages lang)
        {
            // test code
            // test assertions
            // Assert.Equal(expectedResponse, actualResponse, StringComparer.OrdinalIgnoreCase);
        }
        ```
      Ex: When the test has paramters:
       ```
        [Theory]
        [SqlInlineData(0, 0)]
        public async void GetProductsByCostTest(int n, int cost, SupportedLanguages lang)
        {
            // test code
            // test assertions ->
            // Assert.Equal(expectedResponse, actualResponse, StringComparer.OrdinalIgnoreCase);
        }
        ```
    2. Use [UnsupportedLanguages] attribute over the test to specify the list of languages that the test must not run against.
    Ex:
    ```
    [Theory]
        [SqlInlineData()]
        [UnsupportedLanguages(SupportedLanguages.JavaScript)] // Collectors are only available in C#
        public void AddProductsCollectorTest(SupportedLanguages lang)
        {
            // test code
            // test assertions ->
        // Assert.Equal(5000, this.ExecuteScalar("SELECT COUNT(1) FROM Products"));
        }
    ```