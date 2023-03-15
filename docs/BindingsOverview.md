# Azure SQL bindings for Azure Functions - Overview

## Table of Contents

- [Azure SQL bindings for Azure Functions - Overview](#azure-sql-bindings-for-azure-functions---overview)
  - [Table of Contents](#table-of-contents)
  - [Input Binding](#input-binding)
    - [Retry support for Input Bindings](#retry-support-for-input-bindings)
  - [Output Binding](#output-binding)
    - [Primary Key Special Cases](#primary-key-special-cases)
      - [Identity Columns](#identity-columns)
      - [Columns with Default Values](#columns-with-default-values)
    - [Retry support for Output Bindings](#retry-support-for-output-bindings)

## Input Binding

Azure SQL Input bindings take a SQL query or stored procedure to run and returns the output to the function.

### Retry support for Input Bindings

There currently is no retry support for errors that occur for input bindings. If an exception occurs when an input binding is executed then the function code will not be executed. This may result in an error code being returned, for example an HTTP trigger will return a response with a status of 500 to indicate an error occurred.

## Output Binding

Azure SQL Output bindings take a list of rows and upserts them to the user table. Upserting means that if the primary key values of the row already exists in the table, the row is interpreted as an update, meaning that the values of the other columns in the table for that row are updated. If the primary key values do not exist in the table, the row values are inserted as new values. The upserting of the rows is batched by the output binding code.

  > **NOTE:** By default the Output binding uses the T-SQL [MERGE](https://docs.microsoft.com/sql/t-sql/statements/merge-transact-sql) statement which requires [SELECT](https://docs.microsoft.com/sql/t-sql/statements/merge-transact-sql#permissions) permissions on the target database.

### Primary Key Special Cases

Typically Output Bindings require two things :

1. The table being upserted to contains a Primary Key constraint (composed of one or more columns)
2. Each of those columns must be present in the POCO object used in the attribute

Normally either of these are false then an error will be thrown. Below are the situations in which this is not the case :

#### Identity Columns

In the case where one of the primary key columns is an identity column, there are two options based on how the function defines the output object:

1. If the identity column isn't included in the output object then a straight insert is always performed with the other column values. See [AddProductWithIdentityColumn](../samples/samples-csharp/OutputBindingSamples/AddProductWithIdentityColumn.cs) for an example.
2. If the identity column is included (even if it's an optional nullable value) then a merge is performed similar to what happens when no identity column is present. This merge will either insert a new row or update an existing row based on the existence of a row that matches the primary keys (including the identity column). See [AddProductWithIdentityColumnIncluded](../samples/samples-csharp/OutputBindingSamples/AddProductWithIdentityColumnIncluded.cs) for an example.

#### Columns with Default Values

In the case where one of the primary key columns has a default value, there are also two options based on how the function defines the output object:

1. If the column with a default value is not included in the output object, then a straight insert is always performed with the other values. See [AddProductWithDefaultPK](../samples/samples-csharp/OutputBindingSamples/AddProductWithDefaultPK.cs) for an example.
2. If the column with a default value is included then a merge is performed similar to what happens when no default column is present. If there is a nullable column with a default value, then the provided column value in the output object will be upserted even if it is null.

### Retry support for Output Bindings

There currently is no built-in support for errors that occur while executing output bindings. If an exception occurs when an output binding is executed then the function execution will stop. This may result in an error code being returned, for example an HTTP trigger will return a response with a status of 500 to indicate an error occurred.

If using a .NET Function then `IAsyncCollector` can be used, and the function code can handle exceptions thrown by the call to `FlushAsync()`.

See <https://github.com/Azure/Azure-Functions/issues/891> for further information.
