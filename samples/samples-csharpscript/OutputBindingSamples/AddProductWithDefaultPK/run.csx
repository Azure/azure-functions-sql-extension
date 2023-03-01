// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#load "../../Common/product.csx"
#r "Newtonsoft.Json"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

public static ProductWithDefaultPK Run(HttpRequest req, ILogger log, [Sql("dbo.ProductsWithDefaultPK", "SqlConnectionString")] out ProductWithDefaultPK product)
{
    log.LogInformation("C# HTTP trigger function processed a request.");


    string requestBody = new StreamReader(req.Body).ReadToEnd();
    product = JsonConvert.DeserializeObject<ProductWithDefaultPK>(requestBody);

    string responseMessage = string.IsNullOrEmpty(product.Name)
        ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {product.Name}. This HTTP triggered function executed successfully.";

    return product;
}