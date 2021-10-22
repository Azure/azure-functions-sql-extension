// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public static class AddProductMissingColumnsException
    {
        // This output binding should throw an error since the ProductsCostNotNull table does not 
        // allows rows without a Cost value.
        [FunctionName("AddProductMissingColumnsException")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct-missingcolumnsexception")]
            HttpRequest req,
            [Sql("dbo.ProductsCostNotNull", ConnectionStringSetting = "SqlConnectionString")] out ProductMissingColumns product)
        {
            product = new ProductMissingColumns
            {
                Name = "test",
                ProductID = 1
                // Cost is missing
            };
            return new CreatedResult($"/api/addproduct-missingcolumnsexception", product);
        }
    }
}
