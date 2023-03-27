// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
#load "../Common/ProductColumnTypes.csx"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Net;
using Microsoft.AspNetCore.Mvc;

public static ProductColumnTypes Run(HttpRequest req, ILogger log, [Sql("dbo.ProductsColumnTypes", "SqlConnectionString")] out ProductColumnTypes product)
{
    product = new ProductColumnTypes()
    {
        ProductId = int.Parse(req.Query["productId"]),
        BigInt = int.MaxValue,
        Bit = true,
        DecimalType = 1.2345M,
        Money = 1.23M,
        Numeric = 1.2345M,
        SmallInt = 0,
        SmallMoney = 1.23M,
        TinyInt = 1,
        FloatType = 1.2,
        Real = 1.2f,
        Date = DateTime.Now,
        Datetime = DateTime.Now,
        Datetime2 = DateTime.Now,
        DatetimeOffset = DateTime.Now,
        SmallDatetime = DateTime.Now,
        Time = DateTime.Now.TimeOfDay,
        CharType = "test",
        Varchar = "test",
        Nchar = "\u2649",
        Nvarchar = "\u2649",
    };

    return product;
}
