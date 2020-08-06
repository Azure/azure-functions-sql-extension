// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace SqlExtensionSamples.InputBindingSamples
{
    public static class GetProductsString
    {
        [FunctionName("GetProductsString")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-string/{cost}")]
            HttpRequest req,
            [Sql("select * from Products where cost = @Cost",
                CommandType = System.Data.CommandType.Text,
                Parameters = "@Cost={cost}",
                ConnectionStringSetting = "SqlConnectionString")]
            string products)
        {
            // Products is a JSON representation of the returned rows. For example, if there are two returned rows,
            // products could look like:
            // [{"ProductID":1,"Name":"Dress","Cost":100},{"ProductID":2,"Name":"Skirt","Cost":100}]
            return (ActionResult)new OkObjectResult(products);
        }
    }
}
