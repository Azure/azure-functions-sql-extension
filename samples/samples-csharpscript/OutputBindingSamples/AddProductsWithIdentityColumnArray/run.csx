#load "../../product.csx"
#r "Newtonsoft.Json"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

public static ProductWithoutId[] Run(HttpRequest req, ILogger log, [Sql("dbo.Products", "SqlConnectionString")] out ProductWithoutId[] products)
{
    log.LogInformation("C# HTTP trigger function processed a request.");

    products = new[]
            {
                new ProductWithoutId
                {
                    Name = "Cup",
                    Cost = 2
                },
                new ProductWithoutId
                {
                    Name = "Glasses",
                    Cost = 12
                }
            };

    string responseMessage = products.Length > 0
        ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, This HTTP triggered function executed successfully.";

    return products;
}