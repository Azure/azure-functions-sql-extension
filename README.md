SQL Server Extension for Azure Functions
===

This repository contains extension code for SQL Server input and output bindings. Trigger bindings are coming soon.

## Quick Start

## Input Binding Samples
The input binding takes four arguments
- **CommandText**: Passed as a constructor argument to the binding. Represents either a query string or the name of a stored procedure.
- **CommandType**: Specifies whether CommandText is a query (`System.Data.CommandType.Text`) or a stored procedure (`System.Data.CommandType.StoredProcedure`)
- **Parameters**: The parameters to the query/stored procedure. This string must follow the format "@param1=param1,@param2=param2" where @param1 is the name of the parameter and param1 is the parameter value. Each pair of parameter name, parameter value is separated by a comma. Within each pair, the parameter name and value is separated by an equals sign. This means that neither the parameter name nor value can contain "," or "=". To specify a `NULL` parameter value, do "@param1=null,@param2=param2". To specify an empty string as a value, do "@param1=,@param2=param2", i.e. do not put any text after the equals sign of the corresponding parameter name. This argument is auto-resolvable (see Query String examples). 
- **ConnectionStringSetting**: Specifies the name of the app setting that contains the SQL connection string used to connect to a database. The connection string must follow the format specified [here](https://docs.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0). 

The following are valid binding types for the result of the query/stored procedure execution:
- **IEnumerable<T>**:
- **IAsyncEnumerable<T>**:
- **String**:
- **SqlCommand**:

The repo contains examples of each of these binding types [here](https://github.com/Azure/azure-functions-sql-extension/tree/dev/samples/SqlExtensionSamples/InputBindingSamples). A few examples are also included below. 

### Query String
The input binding executes the "select * from Products where Cost = @Cost" query, returning the result as an `IEnumerable<Product>`, where Product is a user-defined POCO. The *Parameters* argument passes the `{cost}` specified in the URL that triggers the function, `getproducts/{cost}`, as the value of the `@Cost` parameter in the query. *CommandType* is set to `System.Data.CommandType.Text`, since the constructor argument of the binding is a raw query. 

```csharp
[FunctionName("GetProducts")]
  public static IActionResult Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts/{cost}")]
      HttpRequest req,
      [Sql("select * from Products where Cost = @Cost",
          CommandType = System.Data.CommandType.Text,
          Parameters = "@Cost={cost}",
          ConnectionStringSetting = "SqlConnectionString")]
      IEnumerable<Product> products)
  {
      return (ActionResult)new OkObjectResult(products);
  }
```

`Product` is a user-defined POCO that follows the structure of the Products table. It represents a row of the Products table, with field names and types copying those of the `Products` table schema. For example, if the Products table has three columns of the form
- **ProductID**: int
- **Name**: varchar
- **Cost**: int

Then the `Product` class would look like
```csharp
public class Product
{
    public int ProductID { get; set; }

    public string Name { get; set; }

    public int Cost { get; set; }

}
```

### Empty Parameter Value
In this case, the parameter value of the `@Name` parameter is an empty string. 
```csharp
[FunctionName("GetProductsNameEmpty")]
  public static IActionResult Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-nameempty/{cost}")]
      HttpRequest req,
      [Sql("select * from Products where Cost = @Cost and Name = @Name",
          CommandType = System.Data.CommandType.Text,
          Parameters = "@Cost={cost},@Name=",
          ConnectionStringSetting = "SqlConnectionString")]
      IEnumerable<Product> products)
  {
      return (ActionResult)new OkObjectResult(products);
  }
  ```

### Null Parameter Value
If the `{name}` specified in the `getproducts-namenull/{name}` URL is "null", the query returns all rows for which the Name column is `NULL`. Otherwise, it returns all rows for which the value of the Name column matches the string passed in `{name}`
```csharp
[FunctionName("GetProductsNameNull")]
  public static IActionResult Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-namenull/{name}")]
      HttpRequest req,
      [Sql("if @Name is null select * from Products where Name is null else select * from Products where @Name = name",
          CommandType = System.Data.CommandType.Text,
          Parameters = "@Name={name}",
          ConnectionStringSetting = "SqlConnectionString")]
      IEnumerable<Product> products)
  {
      return (ActionResult)new OkObjectResult(products);
  }
```

### Stored Procedure
`SelectsProductCost` is the name of a procedure stored in the user's database. In this case, *CommandType* is `System.Data.CommandType.StoredProcedure`. The parameter value of the `@Cost` parameter in the procedure is once again the `{cost}` specified in the `getproducts-storedprocedure/{cost}` URL. 

```csharp
[FunctionName("GetProductsStoredProcedure")]
  public static IActionResult Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-storedprocedure/{cost}")]
      HttpRequest req,
      [Sql("SelectProductsCost",
          CommandType = System.Data.CommandType.StoredProcedure,
          Parameters = "@Cost={cost}",
          ConnectionStringSetting = "SqlConnectionString")]
      IEnumerable<Product> products)
  {
      return (ActionResult)new OkObjectResult(products);
  }
```

### IAsyncEnumerable
Using the `IAsyncEnumerable` binding generally requires that the `Run` function be `async`. It is also important to call `DisposeAsync` at the end of function execution to make sure all resources used by the enumerator are freed. 

```csharp
public static async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-async/{cost}")]
     HttpRequest req,
    [Sql("select * from Products where cost = @Cost",
         CommandType = System.Data.CommandType.Text,
         Parameters = "@Cost={cost}",
         ConnectionStringSetting = "SqlConnectionString")]
     IAsyncEnumerable<Product> products)
{
    var enumerator = products.GetAsyncEnumerator();
    var productList = new List<Product>();
    while (await enumerator.MoveNextAsync())
    {
        productList.Add(enumerator.Current);
    }
    await enumerator.DisposeAsync();
    return (ActionResult)new OkObjectResult(productList);
}
```


# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
