// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#load "../../Common/product.csx"
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
                : "No data passed, Please pass the objects to upsert in the request body.";

    return products;
}