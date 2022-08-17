# Running Performance Tests

## Pre-requisites
The performance tests are based on the IntegrationTestBase class. Follow the instructions to set up the pre-requisites for integration tests [here](https://github.com/Azure/azure-functions-sql-extension/tree/main/test#running-integration-tests).

## Run
The performance tests use BenchmarkDotNet to benchmark performance for input and output bindings.

Run the tests from the terminal.
```
dotnet run
```

## Results
The test results will be generated in the BenchmarkDotNet.Artifacts folder.
