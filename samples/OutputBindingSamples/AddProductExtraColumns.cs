// Copyright (c) Microsoft Corporation. All rights reserved.
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
                Name = "test",
                ProductID = 1,
                Cost = 100,
                ExtraInt = 1,
                ExtraString = "test"
            };
            return new CreatedResult($"/api/addproduct-extracolumns", product);
        }
    }
}
