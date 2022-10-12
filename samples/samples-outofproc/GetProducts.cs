// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc
{
    public static class GetProducts
    {
        [FunctionName("GetProductsOutOfProc")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts/{cost}")]
            HttpRequest req,
            [Sql("select * from Products where Cost = @Cost",
                CommandType = System.Data.CommandType.Text,
                Parameters = "@Cost={cost}",
                ConnectionStringSetting = "SqlConnectionString")]
            IEnumerable<Product> products)
        {
            return new OkObjectResult(products);
        }
    }
}
