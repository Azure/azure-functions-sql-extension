# Azure SQL bindings for Azure Functions - Preview

[![Build Status](https://mssqltools.visualstudio.com/CrossPlatBuildScripts/_apis/build/status/SQL%20Bindings/SQL%20Bindings%20-%20Nightly?branchName=main)](https://mssqltools.visualstudio.com/CrossPlatBuildScripts/_build/latest?definitionId=481&branchName=main)

## Table of Contents
  - [Introduction](#introduction)
  - [Known Issues](#known-issues)
  - [Telemetry](#telemetry)
  - [Trademarks](#trademarks)

## Introduction

This repository contains the Azure SQL bindings for Azure Functions extension code as well as a quick start tutorial and samples illustrating how to use the binding in different ways. The types of bindings supported are:

- **Input Binding**: takes a SQL query to run and returns the output of the query in the function.
- **Output Binding**: takes a list of rows and upserts them into the user table (i.e. If a row doesn't already exist, it is added. If it does, it is updated).
- **Trigger Binding**: monitors the user table for changes (i.e., row inserts, updates, and deletes) and invokes the function with updated rows.

See the language-specific guides for further details on support and usage of the Azure SQL bindings for Azure Functions in the supported languages:

- [.NET (C# in-process)](./docs/BindingsGuide_Dotnet.md)
- [Java](./docs/BindingsGuide_Java.md)
- [Javascript](./docs/BindingsGuide_Javascript.md)
- [Python](./docs/BindingsGuide_Python.md)
- [PowerShell](./docs/BindingsGuide_PowerShell.md)

Further information on the Azure SQL binding for Azure Functions is also available in the [Azure Functions docs](https://docs.microsoft.com/azure/azure-functions/functions-bindings-azure-sql).

## Known Issues

- Output bindings against tables with columns of data types `NTEXT`, `TEXT`, or `IMAGE` are not supported and data upserts will fail. These types [will be removed](https://docs.microsoft.com/sql/t-sql/data-types/ntext-text-and-image-transact-sql) in a future version of SQL Server and are not compatible with the `OPENJSON` function used by this Azure Functions binding.
- Input bindings against tables with columns of data types 'DATETIME', 'DATETIME2', or 'SMALLDATETIME' will assume that the values are in UTC format.

- Trigger bindings will exhibit undefined behavior if the SQL table schema gets modified while the user application is running, for example, if a column is added, renamed or deleted or if the primary key is modified or deleted. In such cases, restarting the application should help resolve any errors.

## Telemetry

This extension collect usage data in order to help us improve your experience. The data is anonymous and doesn't include any personal information. You can opt-out of telemetry by setting the `AZUREFUNCTIONS_SQLBINDINGS_TELEMETRY_OPTOUT` environment variable or the `AzureFunctionsSqlBindingsTelemetryOptOut` app setting (in your `*.settings.json` file) to '1', 'true' or 'yes';

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft’s Trademark & Brand Guidelines](https://www.microsoft.com/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party’s policies.
