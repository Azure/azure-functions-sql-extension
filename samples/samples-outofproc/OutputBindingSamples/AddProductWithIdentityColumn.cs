// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker.Extension.Sql;
using Microsoft.Azure.Functions.Worker;
using System.Web;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.OutputBindingSamples
{
    public static class AddProductWithIdentityColumn
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
        [Function(nameof(AddProductWithIdentityColumn))]
        [SqlOutput("dbo.ProductsWithIdentity", ConnectionStringSetting = "SqlConnectionString")]
        public static ProductWithoutId Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproductwithidentitycolumn")]
            HttpRequestData req)
        {
            var product = new ProductWithoutId
            {
                Name = HttpUtility.ParseQueryString(req.Url.Query)["name"],
                Cost = int.Parse(HttpUtility.ParseQueryString(req.Url.Query)["cost"], null)
            };
            return product;
        }
    }
}
