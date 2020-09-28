# Trigger Binding #

## Introduction ##

**Please note, the SQL trigger binding is still in a developmental state and has not been optimized and thoroughly tested.** This document outlines the current state of the trigger binding as well as a getting started guide and samples of how to use it. The getting started guide details additional setup needed to use trigger bindings and provides a basic tutorial.

At a high level, the trigger binding requires the user specify the name of a table, and in the event a change occurs (i.e. the row is updated, deleted, or inserted), the binding will return the updated rows and values along with any associated metadata.

## Table of Contents ##

***Quickstart:** refer to 'Enable Change Tracking' and 'Set Up Local .NET Function App.'*

- [State of Trigger Bindings](#State-of-Trigger-Bindings)
- [Getting Started](#Getting-Started)
  - [Enable Change Tracking](#Enable-Change-Tracking)
  - [Set Up Local .NET Function App](###Set-Up-Local-.NET-Function-App)
  - [Trigger Binding Tutorial](#Trigger-Binding-Tutorial)
- [Trigger Binding Samples](#Trigger-Binding-Samples)

## State of Trigger Bindings ##

Currently, the SQL trigger binding makes use of SQL Change Tracking in order to determine which rows have been changed. However, SQL Change Tracking only provides the primary keys of changed rows. It doesn't return any additional data about the rows. In order to get the additional row data, worker tables are created which find the changed rows and copy over all data associated with the row. However, in order to scale and allow multiple workers to work on the same table, additional internal tables are created to keep track of what worker tables are working on. In short, all of these additional tables are created in the SQL database and provide significant overhead which affects the amount of storage needed for the trigger binding as well as performance. Additional optimization work needs to be done before the SQL trigger can be considered complete.

## Getting Started ##

### Enable Change Tracking ###

The trigger binding uses SQL's [change tracking functionality](https://docs.microsoft.com/en-us/sql/relational-databases/track-changes/about-change-tracking-sql-server?view=sql-server-ver15) to monitor a user table for changes. As such, it is necessary to enable change tracking on the database and table before using the trigger binding. This can be done in the query editor in the portal. If you need help navigating to it, visit the 'Create Azure SQL Database' section in the README.

1. To enable change tracking on the database, run

    ```sql
    ALTER DATABASE ['your database name']
    SET CHANGE_TRACKING = ON  
    (CHANGE_RETENTION = 2 DAYS, AUTO_CLEANUP = ON)
    ```

    The `CHANGE_RETENTION` parameter specifies for how long changes are kept in the change tracking table. In this case, if a row in a user table hasn't experienced any new changes for two days, it will be removed from the associated change tracking table. The `AUTO_CLEANUP` parameter is used to enable or disable the clean-up task that removes stale data. More information about this command is provided [here](https://docs.microsoft.com/en-us/sql/relational-databases/track-changes/enable-and-disable-change-tracking-sql-server?view=sql-server-ver15#enable-change-tracking-for-a-database).

1. To enable change tracking on the table, run

    ```sql
    ALTER TABLE dbo.Employees
    ENABLE CHANGE_TRACKING  
    WITH (TRACK_COLUMNS_UPDATED = ON)
    ```

    The `TRACK_COLUMNS_UPDATED` feature being enabled means that the change tracking table also stores information about what columns where updated in the case of an `UPDATE`. Currently, the trigger binding does not make use of this additional metadata, though that functionality could be added in the future. More information about this command is provided [here](https://docs.microsoft.com/en-us/sql/relational-databases/track-changes/enable-and-disable-change-tracking-sql-server?view=sql-server-ver15#enable-change-tracking-for-a-table).

    The trigger binding needs to have read access to the table being monitored for changes as well as to the change tracking system tables. It also needs write access to an `az_func` schema within the database, where it will create additional worker tables to process the changes. Each user table will thus have an associated change tracking table and worker table. The worker table will contain roughly as many rows as the change tracking table, and will be cleaned up approximately as often as the change table.

### Local .NET Function App ###

**NOTE: THE MYGET PACKAGE IN THE README DOES NOT CURRENTLY CONTAIN TRIGGER BINDING FUNCTIONALITY**

- Clone the SQL trigger/binding github repo to your local machine
- Open the SQL Binding folder in the src folder in VSCode
- Press 'F1' and search for 'Azure Functions: Create New Project'
- Choose SqlBinding -> C# -> HttpTrigger ->  (Provide a function name) -> Company.namespace -> anonymous
- Replace SqlBinding.csproj with the following

    ```csproj
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>netcoreapp3.1</TargetFramework>
        <AzureFunctionsVersion>v3</AzureFunctionsVersion>
        <Description>SQL binding extension for Azure Functions</Description>
        <Company>Microsoft</Company>
        <Authors>Microsoft</Authors>
        <Product>SQL Binding Extension</Product>
        <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
        <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
        <DebugSymbols>true</DebugSymbols>
        <IncludeSymbols>false</IncludeSymbols>
        <DebugType>embedded</DebugType>
        <PackageId>Microsoft.Azure.WebJobs.Extensions.Sql</PackageId>
        <PublishRepositoryUrl>true</PublishRepositoryUrl>
        <EmbedUntrackedSources>true</EmbedUntrackedSources>
        <Version>1.0.0-preview2</Version>
      </PropertyGroup>

      <ItemGroup>
        <PackageReference Include="Microsoft.NET.Sdk.Functions" Version="3.0.7" />
      </ItemGroup>
      <ItemGroup>
        <PackageReference Include="Microsoft.Azure.WebJobs" Version="3.0.*" />
        <PackageReference Include="Microsoft.Data.SqlClient" Version="2.0.*" />
        <PackageReference Include="Microsoft.SourceLink.GitHub" Version="1.0.*" PrivateAssets="All" />
        <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
          <_Parameter1>SqlExtension.Tests</_Parameter1>
        </AssemblyAttribute>
        <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
          <_Parameter1>DynamicProxyGenAssembly2</_Parameter1>
        </AssemblyAttribute>
      </ItemGroup>
      <ItemGroup>
        <None Update="host.json">
          <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </None>
        <None Update="local.settings.json">
          <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
          <CopyToPublishDirectory>Never</CopyToPublishDirectory>
        </None>
      </ItemGroup>
    </Project>
    ```

- Add the below to 'local.settings.json' in "Values"

    ```csharp
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "AzureWebJobsDashboard": "UseDevelopmentStorage=true"
    ```

- Add your connection string to 'local.settings.json' in "Values"

### Trigger Binding Tutorial ###

This tutorial assumes that you have completed all steps from the Input/Output Binding tutorials.

- Move your input/output binding .cs files to SQLBinding
- Move your 'Employee.cs' file to SQLBinding
- Open your app in VSCode
- Create a new file
- Add the following namespaces

    ```csharp
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;
    using System.Collections.Generic;
    using Microsoft.Azure.WebJobs.Extensions.Sql;
    ```

- Below, add the SQL trigger

    ```csharp
    namespace Company.Function
    {
        public static class EmployeeTrigger
        {
            [FunctionName("EmployeeTrigger")]
            public static void Run(
                [SqlTrigger("[dbo].[Employees]", ConnectionStringSetting = "SqlConnectionString")]
                IEnumerable<SqlChangeTrackingEntry<Employee>> changes,
                ILogger logger)
            {
                foreach (var change in changes)
                {
                    Employee employee = change.Data;
                    logger.LogInformation($"Change occurred to Employee table row: {change.ChangeType}");
                    logger.LogInformation($"EmployeeID: {employee.EmployeeId}, FirstName: {employee.FirstName}, LastName: {employee.FirstName}, Company: {employee.Company}, Team: {employee.Team}");
                }
            }
        }
    }
    ```

- Open your output binding and modify some of the values (e.g. Change Team from 'Functions' to 'Azure SQL'). This will update the row when the code is run.
- Hit 'F5' to run your code. Click the second link to the values in your SQL table. You should see the log update and tell you which row changed and what the data in the row is now.
- Congratulations! You have now successfully used all the SQL bindings!

## Trigger Binding Samples ##

The trigger binding takes two arguments

- **TableName**: Passed as a constructor argument to the binding. Represents the name of the table to be monitored for changes.
- **ConnectionStringSetting**: Specifies the name of the app setting that contains the SQL connection string used to connect to a database. The connection string must follow the format specified [here](https://docs.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0).

The following are valid binding types for trigger binding

- **IEnumerable<SqlChangeTrackingEntry\<T\>>**: Each element is a `SqlChangeTrackingEntry`, which stores change metadata about a modified row in the user table as well as the row itself. In the case that the row was deleted, only the primary key values of the row are populated. The user table row is represented by `T`, where `T` is a user-defined POCO, or Plain Old C# Object. `T` should follow the structure of a row in the queried table. See the [Query String](#query-string) section for an example of what `T` should look like. The two fields of a `SqlChangeTrackingEntry` are the `Data` field of type `T` which stores the row, and the `ChangeType` field of type `SqlChangeType` which indicates the type of operaton done to the row (either an insert, update, or delete).

Any time changes happen to the "Products" table, the function is triggered with a list of changes that occurred. The changes are processed sequentially, so the function will be triggered by the earliest changes first.

```csharp
[FunctionName("ProductsTrigger")]
public static void Run(
    [SqlTrigger("Products", ConnectionStringSetting = "SqlConnectionString")]
    IEnumerable<SqlChangeTrackingEntry<Product>> changes,
    ILogger logger)
{
    foreach (var change in changes)
    {
        Product product = change.Data;
        logger.LogInformation($"Change occurred to Products table row: {change.ChangeType}");
        logger.LogInformation($"ProductID: {product.ProductID}, Name: {product.Name}, Price: {product.Cost}");
    }
}
```

## Contributing ##

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
