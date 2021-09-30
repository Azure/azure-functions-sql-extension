// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using static SqlExtensionSamples.ProductUtilities;

namespace SqlExtensionSamples
{
    public static class AddProduct
    {
        [FunctionName("AddProduct")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct")]
            HttpRequest req,
            [Sql("dbo.Products", ConnectionStringSetting = "SqlConnectionString")] out Product product)
        {
            product = new Product
            {
                Name = req.Query["name"],
                ProductID = int.Parse(req.Query["id"]),
                Cost = int.Parse(req.Query["cost"])
            };
            return new CreatedResult($"/api/addproduct", product);
        }
    }
}
