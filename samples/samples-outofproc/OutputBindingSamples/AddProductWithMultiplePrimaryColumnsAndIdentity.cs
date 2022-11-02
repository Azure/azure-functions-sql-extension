// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Extension.Sql;
using Microsoft.Azure.Functions.Worker;
using System.Web;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.OutputBindingSamples
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
        [Function(nameof(AddProductWithMultiplePrimaryColumnsAndIdentity))]
        [SqlOutput("dbo.ProductsWithMultiplePrimaryColumnsAndIdentity", ConnectionStringSetting = "SqlConnectionString")]
        public static MultiplePrimaryKeyProductWithoutId Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproductwithmultipleprimarycolumnsandidentity")]
            HttpRequestData req)
        {
            var product = new MultiplePrimaryKeyProductWithoutId
            {
                ExternalId = int.Parse(HttpUtility.ParseQueryString(req.Url.Query)["externalId"], null),
                Name = HttpUtility.ParseQueryString(req.Url.Query)["name"],
                Cost = int.Parse(HttpUtility.ParseQueryString(req.Url.Query)["cost"], null)
            };
            return product;
        }
    }
}
