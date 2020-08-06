// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using static SqlExtensionSamples.ProductUtilities;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace SqlExtensionSamples
{
    public static class AddProductsAsyncCollector
    {
        [FunctionName("AddProductsAsyncCollector")]
        public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproducts-asynccollector")] HttpRequest req,
        [Sql("Products", ConnectionStringSetting = "SqlConnectionString")] IAsyncCollector<Product> products)
        {
            List<Product> newProducts = GetNewProducts(5000);
            foreach (var product in newProducts)
            {
                await products.AddAsync(product);
            }
            // Rows are upserted here
            await products.FlushAsync();

            newProducts = GetNewProducts(5000);
            foreach (var product in newProducts)
            {
                await products.AddAsync(product);
            }
            return new CreatedResult($"/api/addproducts-collector", "done");
        }
    }
}
