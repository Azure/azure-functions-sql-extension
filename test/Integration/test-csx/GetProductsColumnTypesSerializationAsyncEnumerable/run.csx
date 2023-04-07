// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#load "../Common/Product.csx"

using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Collections.Generic;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log, IAsyncEnumerable<ProductColumnTypes> products)
{
    // Test different cultures to ensure that serialization/deserialization works correctly for all types.
    // We expect the datetime types to be serialized in UTC format.
    string language = req.Query["culture"];
    if (!string.IsNullOrEmpty(language))
    {
        CultureInfo.CurrentCulture = new CultureInfo(language);
    }

    var productsList = new List<ProductColumnTypes>();
    await foreach (ProductColumnTypes item in products)
    {
        productsList.Add(item);
    }
    return new OkObjectResult(productsList);
}
