#load "../../product.csx"
#r "Newtonsoft.Json"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

public static Product[] Run(HttpRequest req, ILogger log, [Sql("dbo.Products", "SqlConnectionString")] out Product[] products)
{
    log.LogInformation("C# HTTP trigger function processed a request.");


    string requestBody = new StreamReader(req.Body).ReadToEnd();
    products = JsonConvert.DeserializeObject<Product[]>(requestBody);

    string responseMessage = products.Length > 0
        ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, This HTTP triggered function executed successfully.";

    return products;
}