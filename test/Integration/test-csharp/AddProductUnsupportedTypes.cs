// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public static class AddProductUnsupportedTypes
    {
        // This output binding should throw an exception because the target table has unsupported column types.
        [FunctionName("AddProductUnsupportedTypes")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addproduct-unsupportedtypes")]
            [FromBody] ProductUnsupportedTypes prod,
            [Sql("dbo.ProductsUnsupportedTypes", ConnectionStringSetting = "SqlConnectionString")] out ProductUnsupportedTypes product)
        {
            product = prod;
            return new CreatedResult($"/api/addproduct-unsupportedtypes", product);
        }
    }
}
