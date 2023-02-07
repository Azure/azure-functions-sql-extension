// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using DotnetIsolatedTests.Common;

namespace DotnetIsolatedTests
{
    public static class AddProductIncorrectCasing
    {
        // This output binding should throw an error since the casing of the POCO field 'ProductID' and
        // table column name 'ProductId' do not match.
        [Function(nameof(AddProductIncorrectCasing))]
        [SqlOutput("dbo.Products", "SqlConnectionString")]
        public static ProductIncorrectCasing Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct-incorrectcasing")] HttpRequestData req)
        {
            var product = new ProductIncorrectCasing()
            {
                ProductID = 0,
                Name = "test",
                Cost = 100
            };

            return product;
        }
    }
}