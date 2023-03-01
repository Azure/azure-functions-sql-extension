// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#load "../../Common/product.csx"
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
        ? "This HTTP triggered function executed successfully."
                : "No data passed, Please pass the objects to upsert in the request body.";

    return products;
}