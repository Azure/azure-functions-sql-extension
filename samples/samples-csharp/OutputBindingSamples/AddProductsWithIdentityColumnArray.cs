// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples
{
    public static class AddProductsWithIdentityColumnArray
    {
        /// <summary>
        /// This shows an example of a SQL Output binding where the target table has a primary key
        /// which is an identity column. In such a case the primary key is not required to be in
        /// the object used by the binding - it will insert a row with the other values and the
        /// ID will be generated upon insert.
        /// </summary>
        /// <param name="req">The original request that triggered the function</param>
        /// <param name="product">The created Product object</param>
        /// <returns>The CreatedResult containing the new object that was inserted</returns>
        [FunctionName(nameof(AddProductsWithIdentityColumnArray))]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]
            HttpRequest req,
            [Sql("dbo.ProductsWithIdentity", "SqlConnectionString")] out ProductWithoutId[] products)
        {
            products = new[]
            {
                new ProductWithoutId
                {
                    Name = "Cup",
                    Cost = 2
                },
                new ProductWithoutId
                {
                    Name = "Glasses",
                    Cost = 12
                }
            };
            return new CreatedResult($"/api/addproductswithidentitycolumnarray", products);
        }
    }
}
