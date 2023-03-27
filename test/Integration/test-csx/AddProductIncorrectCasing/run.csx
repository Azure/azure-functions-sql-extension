// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
#load "../Common/ProductColumnTypes.csx"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Net;
using Microsoft.AspNetCore.Mvc;

public static ProductIncorrectCasing Run(HttpRequest req, ILogger log, [Sql("dbo.Products", "SqlConnectionString")] out ProductIncorrectCasing product)
{
    product = new ProductIncorrectCasing
    {
        ProductID = 1,
        Name = "test",
        Cost = 1
    };

    return product;
}
