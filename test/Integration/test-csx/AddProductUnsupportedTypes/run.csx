// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
#load "../Common/Product.csx"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Net;
using Microsoft.AspNetCore.Mvc;

public static ProductUnsupportedTypes Run(HttpRequest req, ILogger log, [Sql("dbo.ProductsUnsupportedTypes", "SqlConnectionString")] out ProductUnsupportedTypes product)
{
    product = new ProductUnsupportedTypes()
    {
        ProductId = 1,
        TextCol = "test",
        NtextCol = "test",
        ImageCol = new byte[] { 1, 2, 3 }
    };

    return product;
}
