// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples
{
    public static class GetProductsColumnTypesSerialization
    {
        /// <summary>
        /// This function verifies that serializing an item with various data types
        /// works as expected.
        /// Note this uses IAsyncEnumerable because IEnumerable serializes the entire table directly,
        /// instead of each item one by one (which is where issues can occur)
        /// </summary>
        [FunctionName(nameof(GetProductsColumnTypesSerialization))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-columntypesserialization")]
            HttpRequest req,
            [Sql("SELECT * FROM [dbo].[ProductsColumnTypes]",
                CommandType = System.Data.CommandType.Text,
                ConnectionStringSetting = "SqlConnectionString")]
            IAsyncEnumerable<ProductColumnTypes> products,
            ILogger log)
        {
            await foreach (ProductColumnTypes item in products)
            {
                log.LogInformation(JsonSerializer.Serialize(item));
            }
            return new OkObjectResult(products);
        }
    }
}
