// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public static class AddProductIncludeIdentity
    {

        /// <summary>
        /// This output binding should throw an Exception because the table specifies ProductID
        /// as an identity column and so can't be included in the object being added.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="product"></param>
        /// <returns></returns>
        [FunctionName(nameof(AddProductIncludeIdentity))]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = nameof(AddProductIncludeIdentity))]
            HttpRequest req,
            [Sql("dbo.ProductsWithIdentity", ConnectionStringSetting = "SqlConnectionString")] out ProductIncludeIdentity product)
        {
            product = new ProductIncludeIdentity
            {
                Name = req.Query["name"],
                ProductID = int.Parse(req.Query["productId"]),
                Cost = int.Parse(req.Query["cost"]),
            };
            return new CreatedResult($"/api/addproduct-includeidentity", product);
        }
    }
}
