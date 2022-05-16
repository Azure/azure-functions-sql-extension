// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples
{
    public static class AddProductsArray
    {
        [FunctionName("AddProductsArray")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addproducts-array")]
            [FromBody] List<Product> products,
            [Sql("dbo.Products", ConnectionStringSetting = "SqlConnectionString")] out Product[] output)
        {
            // Suppose that the ProductID column is the primary key in the Products table, and the
            // table already contains a row with ProductID = 1. In that case, the row will be updated
            // instead of inserted to have values Name = "Cup" and Cost = 2.
            output = products.ToArray();
            return new CreatedResult($"/api/addproducts-array", output);
        }
    }
}
