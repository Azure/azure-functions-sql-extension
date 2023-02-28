// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Azure.Functions.Worker;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.InputBindingSamples
{
    /// <summary>
    /// This shows an example of a SQL Input binding that queries from a SQL View named ProductNames.
    /// </summary>
    public static class GetProductNamesView
    {
        [Function("GetProductNamesView")]
        public static IEnumerable<ProductName> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproduct-namesview/")]
            HttpRequestData req,
            [SqlInput("SELECT * FROM ProductNames",
                "SqlConnectionString")]
            IEnumerable<ProductName> products)
        {
            return products;
        }
    }
}
