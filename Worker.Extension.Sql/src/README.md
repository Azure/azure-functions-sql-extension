# Azure SQL bindings .NET Worker

Welcome to the Azure SQL bindings .NET Worker Repository. The .NET Worker provides .NET 5 support in Azure Functions, introducing an **Isolated Model**, running as an out-of-process language worker that is separate from the Azure Functions runtime. This allows you to have full control over your application's dependencies as well as other new features like a middleware pipeline.

A .NET Isolated function app works differently than a .NET Core 3.1 function app. For .NET Isolated, you build an executable that imports the .NET Isolated language worker as a NuGet package. Your app includes a [`Program.cs`](https://github.com/Azure/azure-functions-sql-extension/blob/main/samples/samples-outofproc/Program.cs) that starts the worker.

## Binding Model

.NET Isolated introduces a new binding model, slightly different from the binding model exposed in .NET Core 3 Azure Functions. More information can be [found here](https://github.com/Azure/azure-functions-dotnet-worker/wiki/.NET-Worker-bindings). Please review our samples for usage [information](#samples).

## Middleware

The Azure Functions .NET Isolated supports middleware registration, following a model similar to what exists in ASP.NET and giving you the ability to inject logic into the invocation pipeline, pre and post function executions.

## Samples

Get started quickly with the samples available in our [repository](https://github.com/Azure/azure-functions-sql-extension/tree/main/samples/samples-outofproc)

For further details on setup, usage and samples of the bindings see the guide below:

- [.NET (C# out-of-proc)](./docs/SetupGuide_DotnetOutOfProc.md)
