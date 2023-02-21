// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using DotnetIsolatedTests.Common;
using System.Globalization;
using System.Threading.Tasks;
using System.Web;

namespace DotnetIsolatedTests
{
    public static class GetProductsColumnTypesSerializationAsyncEnumerable
    {
        /// <summary>
        /// This function verifies that serializing an item with various data types
        /// and different languages works when using IAsyncEnumerable.
        /// </summary>
        [Function(nameof(GetProductsColumnTypesSerializationAsyncEnumerable))]
        public static async Task<List<ProductColumnTypes>> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-columntypesserializationasyncenumerable")]
            HttpRequestData req,
            [SqlInput("SELECT * FROM [dbo].[ProductsColumnTypes]",
                "SqlConnectionString")]
            IAsyncEnumerable<ProductColumnTypes> products)
        {
            // Test different cultures to ensure that serialization/deserialization works correctly for all types.
            // We expect the datetime types to be serialized in UTC format.
            string language = HttpUtility.ParseQueryString(req.Url.Query)["culture"];
            if (!string.IsNullOrEmpty(language))
            {
                CultureInfo.CurrentCulture = new CultureInfo(language);
            }

            var productsList = new List<ProductColumnTypes>();
            await foreach (ProductColumnTypes item in products)
            {
                productsList.Add(item);
            }
            return productsList;
        }
    }
}
