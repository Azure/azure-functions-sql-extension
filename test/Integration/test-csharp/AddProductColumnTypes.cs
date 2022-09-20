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
                [Sql("dbo.ProductsColumnTypes", ConnectionStringSetting = "SqlConnectionString")] out ProductColumnTypes product)
        {
            product = new ProductColumnTypes()
            {
                ProductID = int.Parse(req.Query["productId"]),
                Datetime = DateTime.UtcNow,
                Datetime2 = DateTime.UtcNow
            };

            // Items were inserted successfully so return success, an exception would be thrown if there
            // was any issues
            return new OkObjectResult("Success!");
        }
    }
}
