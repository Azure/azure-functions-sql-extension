// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using static SqlExtensionSamples.ProductUtilities;

namespace SqlExtensionSamples
{
    public static class GetProductsNameEmpty
    {
        // In this example, the value passed to the @Name parameter is an empty string
        [FunctionName("GetProductsNameEmpty")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-nameempty/{cost}")]
            HttpRequest req,
            [Sql("select * from Products where Cost = @Cost and Name = @Name",
                CommandType = System.Data.CommandType.Text,
                Parameters = "@Cost={cost},@Name=",
                ConnectionStringSetting = "SqlConnectionString")]
            IEnumerable<Product> products)
        {
            return (ActionResult)new OkObjectResult(products);
        }
    }
}
