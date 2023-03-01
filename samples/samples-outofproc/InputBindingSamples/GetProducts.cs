// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;



namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.InputBindingSamples
{
    public static class GetProducts
    {
        [Function("GetProducts")]
        public static IEnumerable<Product> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts/{cost}")]
            HttpRequestData req,
            [SqlInput("select * from Products where Cost = @Cost",
                "SqlConnectionString",
                parameters: "@Cost={cost}")]
            IEnumerable<Product> products)
        {
            return products;
        }
    }
}
