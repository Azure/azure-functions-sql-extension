// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples
{

    public static class AddProductWithDefaultPK
    {
        /// <summary>
        /// This shows an example of a SQL Output binding where the target table has a default primary key
        /// of type uniqueidentifier and the column is not included in the output object. A new row will
        /// be inserted and the uniqueidentifier will be generated by the engine.
        /// </summary>
        /// <param name="req">The original request that triggered the function</param>
        /// <param name="product">The created ProductWithDefaultPK object</param>
        /// <returns>The CreatedResult containing the new object that was inserted</returns>
        [FunctionName(nameof(AddProductWithDefaultPK))]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addproductwithdefaultpk")]
            [FromBody] ProductWithDefaultPK product,
            [Sql("dbo.ProductsWithDefaultPK", ConnectionStringSetting = "SqlConnectionString")] out ProductWithDefaultPK output)
        {
            output = product;
            return new CreatedResult($"/api/addproductwithdefaultpk", output);
        }
    }
}