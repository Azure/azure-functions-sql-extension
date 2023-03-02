// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#load "../../Common/product.csx"
#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System.Collections.Generic;

public static IActionResult Run(HttpRequest req, ILogger log, IEnumerable<Product> products)
{
    log.LogInformation("C# HTTP trigger function processed a request.");
    return new OkObjectResult(products);
}
