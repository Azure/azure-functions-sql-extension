# Azure SQL bindings for Azure Functions - Preview

## Table of Contents

- [Azure SQL bindings for Azure Functions - Preview](#azure-sql-bindings-for-azure-functions---preview)
  - [Table of Contents](#table-of-contents)
  - [Introduction](#introduction)
  - [Supported SQL Server Versions](#supported-sql-server-versions)
  - [Known Issues](#known-issues)
    - [Output Bindings](#output-bindings)
    - [Trigger Bindings](#trigger-bindings)
  - [Telemetry](#telemetry)
  - [Trademarks](#trademarks)

## Introduction

This repository contains the Azure SQL bindings for Azure Functions extension code as well as a quick start tutorial and samples illustrating how to use the binding in different ways. The types of bindings supported are:

- **Input Binding**: takes a SQL query or stored procedure to run and returns the output to the function.
- **Output Binding**: takes a list of rows and upserts them into the user table (i.e. If a row doesn't already exist, it is added. If it does, it is updated).
- **Trigger Binding**: monitors the user table for changes (i.e., row inserts, updates, and deletes) and invokes the function with updated rows.

For a more detailed overview of the different types of bindings see the [Bindings Overview](https://github.com/Azure/azure-functions-sql-extension/blob/main/docs/BindingsOverview.md).

For further details on setup, usage and samples of the bindings see the language-specific guides below:

- [.NET (C# in-process)](https://github.com/Azure/azure-functions-sql-extension/blob/main/docs/SetupGuide_Dotnet.md)
- [.NET (C# out-of-proc)](https://github.com/Azure/azure-functions-sql-extension/blob/main/docs/SetupGuide_DotnetOutOfProc.md)
- [Java](https://github.com/Azure/azure-functions-sql-extension/blob/main/docs/SetupGuide_Java.md)
- [Javascript](https://github.com/Azure/azure-functions-sql-extension/blob/main/docs/SetupGuide_Javascript.md)
- [PowerShell](https://github.com/Azure/azure-functions-sql-extension/blob/main/docs/SetupGuide_PowerShell.md)
- [Python](https://github.com/Azure/azure-functions-sql-extension/blob/main/docs/SetupGuide_Python.md)

Further information on the Azure SQL binding for Azure Functions is also available in the [docs](https://aka.ms/sqlbindings).

## Supported SQL Server Versions

This extension uses the [OPENJSON](https://learn.microsoft.com/sql/t-sql/functions/openjson-transact-sql) statement which requires a database compatibility level of 130 or higher (2016 or higher). To view or change the compatibility level of your database, see [this documentation article](https://learn.microsoft.com/sql/relational-databases/databases/view-or-change-the-compatibility-level-of-a-database) for more information.

Databases on SQL Server, Azure SQL Database, or Azure SQL Managed Instance which meet the compatibility level requirement above are supported.

## Known Issues

- The table used by a SQL binding or SQL trigger cannot contain two columns that only differ by casing (Ex. 'Name' and 'name').
- Non-CSharp functions using SQL bindings against tables with columns of data types `BINARY` or `VARBINARY` need to map those columns to a string type. Input bindings will return the binary value as a base64 encoded string. Output bindings require the value upserted to binary columns to be a base64 encoded string.
- SQL bindings against tables with columns of data types `GEOMETRY` and `GEOGRAPHY` are not supported. Issue is tracked [here](https://github.com/Azure/azure-functions-sql-extension/issues/654).

### Output Bindings

- Output bindings against tables with columns of data types `NTEXT`, `TEXT`, or `IMAGE` are not supported and data upserts will fail. These types [will be removed](https://docs.microsoft.com/sql/t-sql/data-types/ntext-text-and-image-transact-sql) in a future version of SQL Server and are not compatible with the `OPENJSON` function used by this Azure Functions binding.
- .NET In-Proc output bindings against tables with columns of data types `DATE`, `DATETIME`, `DATETIME2`, `DATETIMEOFFSET`, or `SMALLDATETIME` will convert values for those columns to ISO8061 format ("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffZ") before upsertion. This does not happen for functions written in C# out-of-proc or other languages.
- Output bindings execution order is not deterministic ([azure-webjobs-sdk#1025](https://github.com/Azure/azure-webjobs-sdk/issues/1025)) and so the order that data is upserted is not guaranteed. This can be problematic if, for example, you upsert rows to two separate tables with one having a foreign key reference to another. The upsert will fail if the dependent table does its upsert first.

    Some options for working around this :
  - Have multiple functions, with dependent functions being triggered by the initial functions (through a trigger binding or other such method)
  - Use [dynamic (imperative)](https://learn.microsoft.com/azure/azure-functions/functions-bindings-expressions-patterns#binding-at-runtime) bindings (.NET only)
  - Use [IAsyncCollector](https://learn.microsoft.com/azure/azure-functions/functions-dotnet-class-library?tabs=v2%2Ccmd#writing-multiple-output-values) and call `FlushAsync` in the order desired (.NET only)
- For PowerShell Functions that use hashtables must use the `[ordered]@` for the request query or request body assertion in order to upsert the data to the SQL table properly. An example can be found [here](https://github.com/Azure/azure-functions-sql-extension/blob/main/samples/samples-powershell/AddProductsWithIdentityColumnArray/run.ps1).
- Java, PowerShell, and Python Functions using Output bindings cannot pass in null or empty values via the query string.
  - Java: Issue is tracked [here](https://github.com/Azure/azure-functions-java-worker/issues/683).
  - PowerShell: The workaround is to use the `$TriggerMetadata[$keyName]` to retrieve the query property - an example can be found [here](https://github.com/Azure/azure-functions-sql-extension/blob/main/samples/samples-powershell/AddProductParams/run.ps1). Issue is tracked [here](https://github.com/Azure/azure-functions-powershell-worker/issues/895).
  - Python: The workaround is to use `parse_qs` - an example can be found [here](https://github.com/Azure/azure-functions-sql-extension/blob/main/samples/samples-python/AddProductParams/__init__.py). Issue is tracked [here](https://github.com/Azure/azure-functions-python-worker/issues/894).

### Trigger Bindings

- Trigger bindings will exhibit undefined behavior if the SQL table schema gets modified while the user application is running, for example, if a column is added, renamed or deleted or if the primary key is modified or deleted. In such cases, restarting the application should help resolve any errors.

## Telemetry

This extension collect usage data in order to help us improve your experience. The data is anonymous and doesn't include any personal information. You can opt-out of telemetry by setting the `AZUREFUNCTIONS_SQLBINDINGS_TELEMETRY_OPTOUT` environment variable or the `AzureFunctionsSqlBindingsTelemetryOptOut` app setting (in your `*.settings.json` file) to '1', 'true' or 'yes';

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft’s Trademark & Brand Guidelines](https://www.microsoft.com/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party’s policies.
