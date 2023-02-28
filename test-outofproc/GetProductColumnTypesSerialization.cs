// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Azure.Functions.Worker;
using DotnetIsolatedTests.Common;

namespace DotnetIsolatedTests
{
    public static class GetProductsColumnTypesSerialization
    {
        /// <summary>
        /// This function verifies that serializing an item with various data types
        /// works as expected when using IEnumerable.
        /// </summary>
        [Function(nameof(GetProductsColumnTypesSerialization))]
        public static IEnumerable<ProductColumnTypes> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-columntypesserialization")]
            HttpRequest req,
            [SqlInput("SELECT * FROM [dbo].[ProductsColumnTypes]",
                "SqlConnectionString")]
            IEnumerable<ProductColumnTypes> products)
        {
            return products;
        }
    }
}
