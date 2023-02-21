// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.MultipleBindingsSamples
{
    /// <summary>
    /// This function uses a SQL input binding to get products from the Products table
    /// and upsert those products to the ProductsWithIdentity table.
    /// </summary>
    public static class GetAndAddProducts
    {
        [FunctionName("GetAndAddProducts")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getandaddproducts/{cost}")]
            HttpRequest req,
            [Sql("SELECT * FROM Products",
                "SqlConnectionString",
                parameters: "@Cost={cost}")]
            IEnumerable<Product> products,
            [Sql("ProductsWithIdentity",
                "SqlConnectionString")]
            out Product[] productsWithIdentity)
        {
            productsWithIdentity = products.ToArray();

            return new OkObjectResult(products);
        }
    }
}
