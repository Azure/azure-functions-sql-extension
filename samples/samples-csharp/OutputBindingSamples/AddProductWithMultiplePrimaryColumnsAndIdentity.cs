// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples
{
    public class MultiplePrimaryKeyProductWithoutId
    {
        public int ExternalId { get; set; }

        public string Name { get; set; }

        public int Cost { get; set; }
    }

    public static class AddProductWithMultiplePrimaryColumnsAndIdentity
    {
        /// <summary>
        /// This shows an example of a SQL Output binding where the target table has a primary key 
        /// which is comprised of multiple columns, with one of them being an identity column. In 
        /// such a case the identity column is not required to be in the object used by the binding 
        /// - it will insert a row with the other values and the ID will be generated upon insert.
        /// All other primary key columns are required to be in the object.
        /// </summary>
        /// <param name="req">The original request that triggered the function</param>
        /// <param name="product">The created Product object</param>
        /// <returns>The CreatedResult containing the new object that was inserted</returns>
        [FunctionName(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity))]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproductwithmultipleprimarycolumnsandidentity")]
            HttpRequest req,
            [Sql("dbo.ProductsWithMultiplePrimaryColumnsAndIdentity", "SqlConnectionString")] out MultiplePrimaryKeyProductWithoutId product)
        {
            product = new MultiplePrimaryKeyProductWithoutId
            {
                ExternalId = int.Parse(req.Query["externalId"]),
                Name = req.Query["name"],
                Cost = int.Parse(req.Query["cost"])
            };
            return new CreatedResult($"/api/addproductwithmultipleprimarycolumnsandidentity", product);
        }
    }
}
