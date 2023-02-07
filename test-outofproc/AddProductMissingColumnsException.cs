// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using DotnetIsolatedTests.Common;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Http;

namespace DotnetIsolatedTests
{
    public static class AddProductMissingColumnsExceptionFunction
    {
        // This output binding should throw an error since the ProductsCostNotNull table does not
        // allows rows without a Cost value.
        [Function("AddProductMissingColumnsExceptionFunction")]
        [SqlOutput("dbo.ProductsCostNotNull", "SqlConnectionString")]
        public static ProductMissingColumns Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addproduct-missingcolumnsexception")]
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
