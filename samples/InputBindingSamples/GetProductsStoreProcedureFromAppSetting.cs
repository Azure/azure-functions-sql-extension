// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples
{

    public static class GetProductsStoredProcedureFromAppSetting
    {
        [FunctionName("GetProductsStoredProcedureFromAppSetting")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproductscost100-storedprocedure-appsetting")]
            HttpRequest req,
            [Sql("%Sp_SelectCost%",
                CommandType = System.Data.CommandType.StoredProcedure,
                Parameters = "@Cost=%Cost100%",
                ConnectionStringSetting = "SqlConnectionString")]
            IEnumerable<Product> products)
        {
            return new OkObjectResult(products);
        }
    }
}