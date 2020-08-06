// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using static SqlExtensionSamples.ProductUtilities;

namespace SqlExtensionSamples
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
            output = new Product[2];
            var product = new Product();
            product.ProductID = 1;
            product.Name = "Cup";
            product.Cost = 2;
            output[0] = product;
            product = new Product();
            product.ProductID = 2;
            product.Name = "Glasses";
            product.Cost = 12;
            output[1] = product;
            return new CreatedResult($"/api/addproducts-array", output);
        }
    }
}
