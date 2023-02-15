// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples
{
    public static class GetProductsNameEmpty
    {
        // In this example, the value passed to the @Name parameter is an empty string
        [FunctionName("GetProductsNameEmpty")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-nameempty/{cost}")]
            HttpRequest req,
            [Sql("select * from Products where Cost = @Cost and Name = @Name",
                "SqlConnectionString",
                parameters: "@Cost={cost},@Name=")]
            IEnumerable<Product> products)
        {
            return new OkObjectResult(products);
        }
    }
}
