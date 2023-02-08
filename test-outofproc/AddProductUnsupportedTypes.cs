// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using DotnetIsolatedTests.Common;
using Microsoft.AspNetCore.Http;

namespace DotnetIsolatedTests
{
    public static class AddProductUnsupportedTypes
    {
        /// <summary>
        /// This output binding should fail since the target table has unsupported column types.
        /// </summary>
        [Function("AddProductUnsupportedTypes")]
        [SqlOutput("dbo.ProductsUnsupportedTypes", "SqlConnectionString")]
        public static ProductUnsupportedTypes Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct-unsupportedtypes")]
            HttpRequest req)
        {
            var product = new ProductUnsupportedTypes
            {
                ProductId = 1,
                TextCol = "test",
                NtextCol = "test",
                ImageCol = new byte[] { 1, 2, 3 }
            };
            return product;
        }
    }
}
