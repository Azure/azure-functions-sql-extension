// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
#load "../Common/ProductColumnTypes.csx"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Net;
using Microsoft.AspNetCore.Mvc;

public static ProductExtraColumns Run(HttpRequest req, ILogger log, [Sql("dbo.Products", "SqlConnectionString")] out ProductExtraColumns product)
{
    product = new ProductExtraColumns
    {
        Name = "test",
        ProductId = 1,
        Cost = 100,
        ExtraInt = 1,
        ExtraString = "test"
    };

    return product;
}
