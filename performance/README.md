# Running Performance Tests

## Pre-requisites

The performance tests are based on the IntegrationTestBase class. Follow the instructions to set up the pre-requisites for integration tests [here](../test/README.md#running-integration-tests).

## Run

The performance tests use BenchmarkDotNet to benchmark performance for input and output bindings.

Run the tests from the terminal.

### Run all tests

```bash
cd performance
dotnet run -c Release
```

### Run subset of tests

You can also pass in an argument to the test runner to run only a subset of tests. Each argument corresponds to a category of tests (each contained in a single class). See [SqlBindingBenchmark](./SqlBindingBenchmarks.cs) for the full list of arguments.

```bash
cd performance
dotnet run -c Release input
```

## Results

The test results will be generated in the BenchmarkDotNet.Artifacts folder.
