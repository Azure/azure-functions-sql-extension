// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public static class AddProductMissingColumns
    {
        // This output binding should successfully add the ProductMissingColumns object
        // to the SQL table.
        [FunctionName("AddProductMissingColumns")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addproduct-missingcolumns")]
            HttpRequest req,
            [Sql("dbo.Products", "SqlConnectionString")] out ProductMissingColumns product)
        {
            product = new ProductMissingColumns
            {
                Name = "test",
                ProductId = 1
                // Cost is missing
            };
            return new CreatedResult($"/api/addproduct-missingcolumns", product);
        }
    }
}
