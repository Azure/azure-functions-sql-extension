// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples
{

    public static class AddProductDefaultPKAndDifferentColumnOrder
    {
        /// <summary>
        /// This shows an example of a SQL Output binding where the target table has a default primary key
        /// of type uniqueidentifier and the column is not included in the output object. The order of the
        /// properties in the POCO is different from the order of the columns in the SQL table. A new row will
        /// be inserted and the uniqueidentifier will be generated by the engine.
        /// </summary>
        /// <param name="req">The original request that triggered the function</param>
        /// <param name="product">The created ProductDefaultPKAndDifferentColumnOrder object</param>
        /// <returns>The CreatedResult containing the new object that was inserted</returns>
        [FunctionName("AddProductDefaultPKAndDifferentColumnOrder")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproductdefaultpkanddifferentcolumnorder")]
            HttpRequest reg,
            [Sql("dbo.ProductsWithDefaultPK", "SqlConnectionString")] out ProductDefaultPKAndDifferentColumnOrder output)
        {
            output = new ProductDefaultPKAndDifferentColumnOrder
            {
                Cost = 100,
                Name = "test"
            };
            return new CreatedResult($"/api/addproductdefaultpkanddifferentcolumnorder", output);
        }
    }
}