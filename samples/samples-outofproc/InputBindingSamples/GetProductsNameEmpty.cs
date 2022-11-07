// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker.Extension.Sql;
using Microsoft.Azure.Functions.Worker;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.InputBindingSamples
{
    public static class GetProductsNameEmpty
    {
        // In this example, the value passed to the @Name parameter is an empty string
        [Function("GetProductsNameEmpty")]
        public static IEnumerable<Product> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-nameempty/{cost}")]
            HttpRequest req,
            [SqlInput("select * from Products where Cost = @Cost and Name = @Name",
                CommandType = System.Data.CommandType.Text,
                Parameters = "@Cost={cost},@Name=",
                ConnectionStringSetting = "SqlConnectionString")]
            IEnumerable<Product> products)
        {
            return products;
        }
    }
}
