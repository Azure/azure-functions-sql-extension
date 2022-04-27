﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples
{
    public static class AddProductsArray
    {
        [FunctionName("AddProductsArray")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproducts-array")]
            HttpRequest req,
            [Sql("dbo.Products", ConnectionStringSetting = "SqlConnectionString")] out Product[] output)
        {
            // Suppose that the ProductID column is the primary key in the Products table, and the
            // table already contains a row with ProductID = 1. In that case, the row will be updated
            // instead of inserted to have values Name = "Cup" and Cost = 2.
            output = new[]
            {
                new Product
                {
                    ProductID = 1,
                    Name = "Cup",
                    Cost = 2
                },
                new Product
                {
                    ProductID = 2,
                    Name = "Glasses",
                    Cost = 12
                }
            };
            return new CreatedResult($"/api/addproducts-array", output);
        }
    }
}
