﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Azure.Functions.Worker;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.InputBindingSamples
{

    public static class GetProductsStoredProcedure
    {
        [Function("GetProductsStoredProcedure")]
        public static IEnumerable<Product> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-storedprocedure/{cost}")]
            HttpRequestData req,
            [SqlInput("SelectProductsCost",
                "SqlConnectionString",
                CommandType = System.Data.CommandType.StoredProcedure,
                Parameters = "@Cost={cost}")]
            IEnumerable<Product> products)
        {
            return products;
        }
    }
}