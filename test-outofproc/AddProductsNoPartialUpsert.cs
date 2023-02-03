// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using DotnetIsolatedTests.Common;

namespace DotnetIsolatedTests
{
    public static class AddProductsNoPartialUpsert
    {
        public const int UpsertBatchSize = 1000;

        // This output binding should throw an error since the ProductsNameNotNull table does not 
        // allows rows without a Name value. No rows should be upserted to the Sql table.
        [Function("AddProductsNoPartialUpsert")]
        [SqlOutput("dbo.ProductsNameNotNull", "SqlConnectionString")]
        public static List<Product> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addproducts-nopartialupsert")]
            HttpRequest req)
        {
            List<Product> newProducts = ProductUtilities.GetNewProducts(UpsertBatchSize);
            foreach (Product product in newProducts)
            {
                newProducts.Add(product);
            }

            var invalidProduct = new Product
            {
                Name = null,
                ProductId = UpsertBatchSize,
                Cost = 100
            };
            newProducts.Add(invalidProduct);

            return newProducts;
        }
    }
}
