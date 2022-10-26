// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker.Extension.Sql;
using Microsoft.Azure.Functions.Worker;


namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc
{
    public static class GetProducts
    {
        [Function("GetProductsOutOfProc")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproductsoutofproc/{cost}")]
            HttpRequest req,
            [SqlInput("select * from Products where Cost = @Cost",
                CommandType = System.Data.CommandType.Text,
                Parameters = "@Cost={cost}",
                ConnectionStringSetting = "SqlConnectionString")]
            IEnumerable<Product> products)
        {
            return new OkObjectResult(products);
        }
    }
}
