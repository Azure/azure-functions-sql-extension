// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using static SqlExtensionSamples.ProductUtilities;

namespace SqlExtensionSamples
{
    public static class AddProductsAsyncCollector
    {
        [FunctionName("AddProductsAsyncCollector")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproducts-asynccollector")]
            HttpRequest req,
            [Sql("dbo.Products", ConnectionStringSetting = "SqlConnectionString")] IAsyncCollector<Product> products)
        {
            List<Product> newProducts = GetNewProducts(5000);
            foreach (Product product in newProducts)
            {
                await products.AddAsync(product);
            }
            // Rows are upserted here
            await products.FlushAsync();

            newProducts = GetNewProducts(5000);
            foreach (Product product in newProducts)
            {
                await products.AddAsync(product);
            }
            return new CreatedResult($"/api/addproducts-collector", "done");
        }
    }
}
