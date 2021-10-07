// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using static SqlExtensionSamples.ProductUtilities;

namespace SqlExtensionSamples
{
    public static class AddProductsCollector
    {
        [FunctionName("AddProductsCollector")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproducts-collector")]
            HttpRequest req,
            [Sql("dbo.Products", ConnectionStringSetting = "SqlConnectionString")] ICollector<Product> products)
        {
            List<Product> newProducts = GetNewProducts(5000);
            foreach (Product product in newProducts)
            {
                products.Add(product);
            }
            return new CreatedResult($"/api/addproducts-collector", "done");
        }
    }
}
