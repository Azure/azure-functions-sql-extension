// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker.Extension.Sql;
using Microsoft.Azure.Functions.Worker;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.InputBindingSamples
{

    public static class GetProductsStoredProcedure
    {
        [Function("GetProductsStoredProcedure")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-storedprocedure/{cost}")]
            HttpRequest req,
            [SqlInput("SelectProductsCost",
                CommandType = System.Data.CommandType.StoredProcedure,
                Parameters = "@Cost={cost}",
                ConnectionStringSetting = "SqlConnectionString")]
            IEnumerable<Product> products)
        {
            return new OkObjectResult(products);
        }
    }
}