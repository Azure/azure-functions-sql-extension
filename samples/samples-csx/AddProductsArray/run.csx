// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#load "../Common/product.csx"
#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

public static Product[] Run(HttpRequest req, ILogger log, out Product[] products)
{
    string requestBody = new StreamReader(req.Body).ReadToEnd();
    products = JsonConvert.DeserializeObject<Product[]>(requestBody);

    return products;
}