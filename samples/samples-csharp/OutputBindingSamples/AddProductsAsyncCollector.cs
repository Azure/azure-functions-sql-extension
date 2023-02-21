// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples
{
    public static class AddProductsAsyncCollector
    {
        [FunctionName("AddProductsAsyncCollector")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproducts-asynccollector")]
            HttpRequest req,
            [Sql("dbo.Products", "SqlConnectionString")] IAsyncCollector<Product> products)
        {
            List<Product> newProducts = ProductUtilities.GetNewProducts(5000);
            foreach (Product product in newProducts)
            {
                await products.AddAsync(product);
            }
            // Rows are upserted here
            await products.FlushAsync();

            newProducts = ProductUtilities.GetNewProducts(5000);
            foreach (Product product in newProducts)
            {
                await products.AddAsync(product);
            }
            return new CreatedResult($"/api/addproducts-collector", "done");
        }
    }
}
