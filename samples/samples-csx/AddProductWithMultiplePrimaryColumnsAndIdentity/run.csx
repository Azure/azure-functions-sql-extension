// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#load "../Common/product.csx"
#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

public static MultiplePrimaryKeyProductWithoutId Run(HttpRequest req, ILogger log, out MultiplePrimaryKeyProductWithoutId product)
{
    product = product = new MultiplePrimaryKeyProductWithoutId
    {
        ExternalId = int.Parse(req.Query["externalId"]),
        Name = req.Query["name"],
        Cost = int.Parse(req.Query["cost"])
    };

    return product;
}