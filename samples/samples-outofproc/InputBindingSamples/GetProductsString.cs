// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Azure.Functions.Worker;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.InputBindingSamples
{
    public static class GetProductsString
    {
        [Function("GetProductsString")]
        public static string Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproducts-string/{cost}")]
            HttpRequestData req,
            [SqlInput("select * from Products where cost = @Cost",
                CommandType = System.Data.CommandType.Text,
                Parameters = "@Cost={cost}",
                ConnectionStringSetting = "SqlConnectionString")]
            string products)
        {
            // Products is a JSON representation of the returned rows. For example, if there are two returned rows,
            // products could look like:
            // [{"ProductID":1,"Name":"Dress","Cost":100},{"ProductID":2,"Name":"Skirt","Cost":100}]
            return products;
        }
    }
}
