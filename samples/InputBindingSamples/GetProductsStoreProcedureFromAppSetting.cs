// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples
{
    /// <summary>
    /// This shows an example of a SQL Input binding that uses a stored procedure 
    /// from an app setting value to query for Products with a specific cost that is also defined as an app setting value.
    /// </summary>
    public static class GetProductsStoredProcedureFromAppSetting
    {
        [FunctionName("GetProductsStoredProcedureFromAppSetting")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproductsbycost")]
            HttpRequest req,
            [Sql("%Sp_SelectCost%",
                CommandType = System.Data.CommandType.StoredProcedure,
                Parameters = "@Cost=%ProductCost%",
                ConnectionStringSetting = "SqlConnectionString")]
            IEnumerable<Product> products)
        {
            return new OkObjectResult(products);
        }
    }
}