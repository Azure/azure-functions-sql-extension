// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public static class AddProductColumnTypes
    {
        /// <summary>
        /// This function is used to test compatability with converting various data types to their respective
        /// SQL server types.
        /// </summary>
        [FunctionName(nameof(AddProductColumnTypes))]
        public static IActionResult Run(
                [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct-columntypes")] HttpRequest req,
                [Sql("dbo.ProductsColumnTypes", "SqlConnectionString")] out ProductColumnTypes product)
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

            // Items were inserted successfully so return success, an exception would be thrown if there
            // was any issues
            return new OkObjectResult("Success!");
        }
    }
}
