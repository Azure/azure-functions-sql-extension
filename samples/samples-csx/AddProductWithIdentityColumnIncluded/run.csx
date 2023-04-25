// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#load "../Common/product.csx"
#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

public static ProductWithOptionalId Run(HttpRequest req, ILogger log, out ProductWithOptionalId product)
{
    product = product = new ProductWithOptionalId
    {
        Name = req.Query["name"],
        ProductId = string.IsNullOrEmpty(req.Query["productId"]) ? (int?)null : int.Parse(req.Query["productId"]),
        Cost = int.Parse(req.Query["cost"])
    };

    return product;
}