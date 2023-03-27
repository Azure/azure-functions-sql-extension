// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
#load "../Common/ProductColumnTypes.csx"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Net;
using Microsoft.AspNetCore.Mvc;

public static ProductMissingColumns Run(HttpRequest req, ILogger log, [Sql("dbo.Products", "SqlConnectionString")] out ProductMissingColumns product)
{
    product = new ProductMissingColumns
    {
        Name = "test",
        ProductId = 1
        // Cost is missing
    };

    return product;
}
