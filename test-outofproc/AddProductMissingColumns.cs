// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using DotnetIsolatedTests.Common;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Http;

namespace DotnetIsolatedTests
{
    public static class AddProductMissingColumns
    {
        // This output binding should successfully add the ProductMissingColumns object
        // to the SQL table.
        [Function("AddProductMissingColumns")]
        [SqlOutput("dbo.Products", "SqlConnectionString")]
        public static ProductMissingColumns Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addproduct-missingcolumns")]
            HttpRequest req)
        {
            var product = new ProductMissingColumns
            {
                Name = "test",
                ProductId = 1
                // Cost is missing
            };
            return product;
        }
    }
}
