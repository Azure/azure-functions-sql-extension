// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public static class AddProductIncorrectCasing
    {
        // This output binding should throw an error since the casing of the POCO field 'ProductID' and
        // table column name 'ProductId' do not match.
        [FunctionName("AddProductIncorrectCasing")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct-incorrectcasing")]
            HttpRequest req,
            [Sql("dbo.Products", "SqlConnectionString")] out ProductIncorrectCasing product)
        {
            product = new ProductIncorrectCasing
            {
                ProductID = 1,
                Name = "test",
                Cost = 1
            };
            return new CreatedResult($"/api/addproduct-incorrectcasing", product);
        }
    }
}