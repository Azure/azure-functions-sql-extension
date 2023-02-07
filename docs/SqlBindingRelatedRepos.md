# Azure SQL Bindings for Azure Functions

This document contains a list of all repositories related to SQL Bindings.

## Docs

[Azure SQL bindings for Functions | Microsoft Docs](https://aka.ms/sqlbindings)

## SQL Bindings VS Code Extension

This extension enables users to develop Azure Functions with SQL Bindings.

[azuredatastudio/extensions/sql-bindings at main · microsoft/azuredatastudio (github.com)](https://github.com/microsoft/azuredatastudio/tree/main/extensions/sql-bindings)

SQL Tools Service contains the logic for adding a SQL binding to an existing Azure Function.

[sqltoolsservice/src/Microsoft.SqlTools.ServiceLayer/AzureFunctions at main · microsoft/sqltoolsservice (github.com)](https://github.com/microsoft/sqltoolsservice/tree/main/src/Microsoft.SqlTools.ServiceLayer/AzureFunctions)

## Templates

Contains templates for the various types of bindings and supported languages, each under a Sql* folder.

[azure-functions-templates/Functions.Templates/Templates at dev · Azure/azure-functions-templates (github.com)](https://github.com/Azure/azure-functions-templates/tree/dev/Functions.Templates/Templates)

### Instructions for adding new templates

[Azure/azure-functions-templates: Azure functions templates for the azure portal, CLI, and VS (github.com)](https://github.com/Azure/azure-functions-templates#creating-a-dotnet-templates-cs-and-fs)

## Extension Bundle

Currently SQL Bindings is part of the 3.x and 4.x Preview bundles.

[Azure/azure-functions-extension-bundles at v3.x-preview (github.com)](https://github.com/Azure/azure-functions-extension-bundles/tree/v3.x-preview)

[Azure/azure-functions-extension-bundles at v4.x-preview (github.com)](https://github.com/Azure/azure-functions-extension-bundles/tree/v4.x-preview)

## Python SQL Bindings

### Python Library

We define SqlRow and SqlRowList in _sql.py and the SqlConverter in sql.py.

[Azure/azure-functions-python-library: Azure Functions Python SDK (github.com)](https://github.com/Azure/azure-functions-python-library)

### Python Worker

End to end tests for SQL Bindings in the Python Worker.

[Azure/azure-functions-python-worker: Python worker for Azure Functions. (github.com)](https://github.com/Azure/azure-functions-python-worker)

### PowerShell Worker

PowerShell language worker for Azure Functions.
[Azure/azure-functions-powershell-worker: PowerShell worker for Azure Functions. (github.com)](https://github.com/Azure/azure-functions-powershell-worker)