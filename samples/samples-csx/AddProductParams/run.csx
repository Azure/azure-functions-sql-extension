// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#load "../Common/product.csx"
#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

public static Product Run(HttpRequest req, ILogger log, out Product product)
{
    product = new Product
    {
        Name = req.Query["name"],
        ProductId = int.Parse(req.Query["productId"]),
        Cost = int.Parse(req.Query["cost"])
    };

    return product;
}