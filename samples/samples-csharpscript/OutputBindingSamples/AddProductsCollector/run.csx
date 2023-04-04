// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#load "../../Common/product.csx"
#r "Newtonsoft.Json"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public static ICollector<Product> Run(HttpRequest req, ILogger log, [Sql("dbo.Products", "SqlConnectionString")] ICollector<Product> products)
{
    log.LogInformation("C# HTTP trigger function processed a request.");

    List<Product> newProducts = ProductUtilities.GetNewProducts(5000);
    foreach (Product product in newProducts)
    {
        products?.Add(product);
    }

    return products;
}