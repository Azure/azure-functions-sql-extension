// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Web;
using System.Collections.Specialized;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using DotnetIsolatedTests.Common;
using System;
using System.Data.SqlTypes;

namespace DotnetIsolatedTests
{
    public static class AddProductColumnTypes
    {
        /// <summary>
        /// This function is used to test compatability with converting various data types to their respective
        /// SQL server types.
        /// </summary>
        [Function(nameof(AddProductColumnTypes))]
        [SqlOutput("dbo.ProductsColumnTypes", "SqlConnectionString")]
        public static ProductColumnTypes Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct-columntypes")] HttpRequestData req)
        {
            NameValueCollection queryStrings = HttpUtility.ParseQueryString(req.Url.Query);
            var product = new ProductColumnTypes()
            {
                ProductId = int.Parse(queryStrings["productId"], null),
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
                Date = DateTime.UtcNow,
                Datetime = new SqlDateTime(DateTime.UtcNow).Value,
                Datetime2 = DateTime.UtcNow,
                DatetimeOffset = DateTime.UtcNow,
                SmallDatetime = new SqlDateTime(DateTime.UtcNow).Value,
                Time = DateTime.UtcNow.TimeOfDay,
                CharType = "test",
                Varchar = "test",
                Nchar = "\u2649",
                Nvarchar = "\u2649",
            };
            return product;
        }
    }
}
