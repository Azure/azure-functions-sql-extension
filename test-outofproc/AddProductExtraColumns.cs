// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using DotnetIsolatedTests.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;

namespace DotnetIsolatedTests
{
    public static class AddProductExtraColumns
    {
        // This output binding should throw an Exception because the ProductExtraColumns object has 
        // two properties that do not exist as columns in the SQL table (ExtraInt and ExtraString).
        [Function("AddProductExtraColumns")]
        [SqlOutput("dbo.Products", "SqlConnectionString")]
        public static ProductExtraColumns Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addproduct-extracolumns")]
            HttpRequest req)
        {
            var product = new ProductExtraColumns
            {
                Name = "test",
                ProductId = 1,
                Cost = 100,
                ExtraInt = 1,
                ExtraString = "test"
            };
            return product;
        }
    }
}
