// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;
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
        /// works as expected when using IEnumerable.
        /// </summary>
        [FunctionName(nameof(GetProductsColumnTypesSerialization))]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-columntypesserialization")]
            HttpRequest req,
            [Sql("SELECT * FROM [dbo].[ProductsColumnTypes]",
                CommandType = System.Data.CommandType.Text,
                ConnectionStringSetting = "SqlConnectionString")]
            IEnumerable<ProductColumnTypes> products,
            ILogger log)
        {
            foreach (ProductColumnTypes item in products)
            {
                log.LogInformation(JsonSerializer.Serialize(item));
            }

            return new OkObjectResult(products);
        }
    }
}
