// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public static class AddProductExtraColumns
    {
        // This output binding should throw an Exception because the ProductExtraColumns object has 
        // two properties that do not exist as columns in the SQL table (ExtraInt and ExtraString).
        [FunctionName("AddProductExtraColumns")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addproduct-extracolumns")]
            HttpRequest req,
            [Sql("dbo.Products", "SqlConnectionString")] out ProductExtraColumns product)
        {
            product = new ProductExtraColumns
            {
                Name = "test",
                ProductId = 1,
                Cost = 100,
                ExtraInt = 1,
                ExtraString = "test"
            };
            return new CreatedResult($"/api/addproduct-extracolumns", product);
        }
    }
}
