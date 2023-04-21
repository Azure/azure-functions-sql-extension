// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#load "../../Common/product.csx"
#r "Newtonsoft.Json"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

public static ProductWithOptionalId Run(HttpRequest req, ILogger log, [Sql("dbo.ProductsWithIdentity", "SqlConnectionString")] out ProductWithOptionalId product)
{
    log.LogInformation("C# HTTP trigger function processed a request.");

    product = product = new ProductWithOptionalId
    {
        Name = req.Query["name"],
        ProductId = string.IsNullOrEmpty(req.Query["productId"]) ? (int?)null : int.Parse(req.Query["productId"]),
        Cost = int.Parse(req.Query["cost"])
    };

    string responseMessage = string.IsNullOrEmpty(product.Name)
        ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {product.Name}. This HTTP triggered function executed successfully.";

    return product;
}