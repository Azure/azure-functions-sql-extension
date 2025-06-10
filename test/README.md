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
3. A SQL Server instance - This is used by tests to verify that data is correctly added/fetched from the database when a test Function is run. You just need the server to be up and running, the tests will create the database and tables which will be cleaned up afterwards.

     ### Local Install
     To use a SQL Server installation, ensure `localhost` is available for connection via integrated security.

     ### Docker Container
     Start a SQL Server instance with Docker
     ```
     docker pull mcr.microsoft.com/mssql/server:2019-latest
     docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD={your_password}" -e "MSSQL_PID=Express" -p 1433:1433 --name sql1 -h sql1 -d mcr.microsoft.com/mssql/server:2019-latest
     ```
     After the Docker image is running, you just need to set `SA_PASSWORD` environment variable to `{your_password}` and can run tests normally.

     Note: If `SA_PASSWORD` is not set, the tests will assume you're using a local MSSQL installation and default to using integrated auth. MSSQL on Docker does not support integrated auth by default.

     ### Azure SQL Database
     To use an Azure SQL Database, set the `TEST_CONNECTION_STRING` environment variable to your Azure SQL Database connection string.

### Extension Bundle
   If you've made any changes to the CSharp extension, you will need to run the CopySqlDllToExtensionBundle.Ps1 to update your local extension bundle so that the tests can run against your latest changes.

### Running Java Tests
   Run the BuildJavaProjectsAndRunIntegrationTests.ps1 script in the scripts folder from the root of the repo to build the java projects and run the integration tests.

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

## Troubleshooting Test Failures

This section lists some things to try to help troubleshoot test failures

### Debug Integration tests

You can debug tests locally by following these steps.

The first part is to identify what you want to debug so you know which process to attach to. There are between 2 and 3 different processes to attach to :

#### Test Runner

To debug the test code itself (anything under the `test` folder) you will need to attach to the VS Test Runner that's running the tests.

Visual Studio : Right click on the test in the test explorer and click "Debug"
Visual Studio Code : Install a test explorer extension such as [.NET Core Test Explorer](https://marketplace.visualstudio.com/items?itemName=formulahendry.dotnet-test-explorer), go to the Tests panel, find the `.NET Test Explorer` view, right click the test you want to run and click "Debug Test"

#### Function Host

To debug either any core extension code (anything under the `src` folder) OR function code for .NET In-Process tests you will need to attach to the Function Host that's running the functions.

To do this and be able to set breakpoints before the test runs you will need to follow these steps :

1. Go to [IntegrationTestBase.cs](./Integration/IntegrationTestBase.cs) and in `StartFunctionHost` add a `return;` on the first line - this is to skip having the test start up the function host itself (since you'll be doing it manually)
2. `cd` to the directory containing the functions to run - this will be in the `test/bin/Debug/net9/SqlExtensionSamples/<LANG>` folder (e.g. `test/bin/Debug/net9/SqlExtensionSamples/CSharp`)
3. Run `func host start --functions <FUNCTION_NAME>` - replacing `<FUNCTION_NAME>` with the name of the function you want to debug (e.g. `func host start --functions AddProduct`)
4. Attach to the Function Host process
   * Visual Studio : Use `Attach to Process...`.
   * VS Code you can use `Attach to Function Host` debug target (at the root level of the project)

    **NOTE** If you don't see your breakpoints appear make sure you're using the correct version of the extension DLL. For non-.NET languages this will mean copying over the latest locally built version to the extension bundle as detailed [here](#extension-bundle). If you don't do this the host will be using a different version of the DLL that you won't have symbols for so breakpoints won't be able to load.
5. Run your test

#### Function Worker (Non .NET Isolated Functions only)

For all functions that run in a worker process (which is everything other than .NET In-Process functions) if you need to debug the function code itself then you will need to use the native debugger to attach to the process. The easiest way to do that is to follow these steps:

1. Open up a new instance of VS Code into the samples/test folder containing the function you want to debug (e.g. `samples/samples-powershell` or `test/Integration/test-powershell`)
2. Modify the `.vscode/tasks.json` file for that folder and add `--functions <FUNCTION_NAME>` to the end of the command property for `func: host start`. (e.g. `"command": "host start --functions AddProduct"`)
3. Update the `local.settings.json` in that folder and fill in the value for `SqlConnectionString` (since the test is no longer controlling the startup of the function host)
4. Click F5 to run the `Attach to <LANG> Functions` target, this will launch the specified function and attach the debugger to it
5. Run your test

### Enable debug logging on the Function

Enabling debug logging can greatly increase the information available which can help track down issues or understand at least where the problem may be. To enable debug logging for the Function open [host.json](../samples/samples-csharp/host.json) and add the following property to the `logLevel` section, then rebuild and re-run your test.

```json
"logLevel": {
    "default": "Debug"
}
```

WARNING : Doing this will add a not-insignificant overhead to the test run duration from writing all the additional content to the log files, which may cause timeouts to occur in tests. If this happens you can temporarily increase those timeouts while debug logging is enabled to avoid having unexpected failures.

To enable debug logging in the pipelines set the `AFSQLEXT_TEST_LOGLEVEL` pipeline variable to the desired value (such as `Debug`) and it will use that value when running tests instead of the default.

## General Troubleshooting

### Getting error "OmniSharp.Extensions.JsonRpc.RpcErrorException" when debugging Powershell functions

See https://github.com/microsoft/vscode-azurefunctions/issues/3223 for details and suggestions, but generally updating to Powershell 7.2+ should fix this. Make sure you're setting the default terminal to Powershell 7 and then reloading VS Code before trying to launch and debug the functions.
