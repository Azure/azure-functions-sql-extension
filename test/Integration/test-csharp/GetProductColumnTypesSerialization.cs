// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples
{
    public static class GetProductsColumnTypesSerialization
    {
        /// <summary>
        /// This function verifies that serializing an item with various data types
        /// works as expected when using IEnumerable.
        /// </summary>
        [FunctionName(nameof(GetProductsColumnTypesSerialization))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-columntypesserialization")]
            HttpRequest req,
            [Sql("SELECT * FROM [dbo].[ProductsColumnTypes]",
                CommandType = System.Data.CommandType.Text,
                ConnectionStringSetting = "SqlConnectionString")]
            IEnumerable<ProductColumnTypes> products)
        {
            return new OkObjectResult(products);
        }
    }
}
