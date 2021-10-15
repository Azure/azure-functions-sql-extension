// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples
{
    public static class AddProductExtraColumns
    {
        [FunctionName("AddProductExtraColumns")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct-extracolumns")]
            HttpRequest req,
            [Sql("dbo.Products", ConnectionStringSetting = "SqlConnectionString")] out ProductExtraColumns product)
        {
            product = new ProductExtraColumns
            {
                Name = req.Query["name"],
                ProductID = int.Parse(req.Query["id"]),
                Cost = int.Parse(req.Query["cost"]),
                ExtraInt = int.Parse(req.Query["extraint"]),
                ExtraString = req.Query["extrastring"]
            };
            return new CreatedResult($"/api/addproduct-extracolumns", product);
        }
    }
}
