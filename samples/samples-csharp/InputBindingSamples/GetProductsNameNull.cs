// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples
{
    public static class GetProductsNameNull
    {
        // In this example, if {name} is "null", then the value attached to the @Name parameter is null.
        // This means the input binding returns all products for which the Name column is null.
        // Otherwise, {name} is interpreted as a string, and the input binding returns all products
        // for which the Name column is equal to that string value
        [FunctionName("GetProductsNameNull")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-namenull/{name}")]
            HttpRequest req,
            [Sql("if @Name is null select * from Products where Name is null else select * from Products where @Name = name",
                "SqlConnectionString",
                parameters: "@Name={name}")]
            IEnumerable<Product> products)
        {
            return new OkObjectResult(products);
        }
    }
}
