// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Azure.Functions.Worker;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.MultipleBindingsSamples
{
    /// <summary>
    /// This function uses a SQL input binding to get products from the Products table
    /// and upsert those products to the ProductsWithIdentity table.
    /// </summary>
    public static class GetAndAddProducts
    {
        [Function("GetAndAddProducts")]
        [SqlOutput("ProductsWithIdentity", "SqlConnectionString")]
        public static IEnumerable<Product> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getandaddproducts/{cost}")]
            HttpRequestData req,
            [SqlInput("SELECT * FROM Products",
                "SqlConnectionString",
                parameters: "@Cost={cost}")]
            IEnumerable<Product> products)
        {
            return products.ToArray();
        }
    }
}
