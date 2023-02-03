// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using DotnetIsolatedTests.Common;
using System.Threading.Tasks;

namespace DotnetIsolatedTests
{
    public static class AddProductUnsupportedTypes
    {
        /// <summary>
        /// This output binding should fail since the target table has unsupported column types.
        /// </summary>
        [Function("AddProductUnsupportedTypes")]
        [SqlOutput("dbo.ProductsUnsupportedTypes", ConnectionStringSetting = "SqlConnectionString")]
        public static async Task<ProductUnsupportedTypes> RunAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addproduct-unsupportedtypes")]
            HttpRequestData req)
        {
            ProductUnsupportedTypes product = await req.ReadFromJsonAsync<ProductUnsupportedTypes>();
            return product;
        }
    }
}
