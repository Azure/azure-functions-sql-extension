// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
#load "../Common/Product.csx"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Net;
using Microsoft.AspNetCore.Mvc;

public static ProductDefaultPKAndDifferentColumnOrder Run(HttpRequest req, ILogger log, [Sql("dbo.ProductsWithDefaultPK", "SqlConnectionString")] out ProductDefaultPKAndDifferentColumnOrder product)
{
    product = new ProductDefaultPKAndDifferentColumnOrder
    {
        Cost = 100,
        Name = "test"
    };

    return product;
}
