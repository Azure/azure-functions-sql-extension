﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker.Extension.Sql;
using Microsoft.Azure.Functions.Worker;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.InputBindingSamples
{
    public static class GetProductsTopN
    {
        [Function("GetProductsTopN")]
        public static IEnumerable<Product> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproductstopn/{count}")]
            HttpRequest req,
            [SqlInput("SELECT TOP(CAST(@Count AS INT)) * FROM Products",
                CommandType = System.Data.CommandType.Text,
                Parameters = "@Count={count}",
                ConnectionStringSetting = "SqlConnectionString")]
            IEnumerable<Product> products)
        {
            return products;
        }
    }
}
