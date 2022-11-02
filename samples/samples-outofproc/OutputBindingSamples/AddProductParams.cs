// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker.Extension.Sql;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Web;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.OutputBindingSamples
{
    public static class AddProductParams
    {
        [Function("AddProductParams")]
        [SqlOutput("dbo.Products", ConnectionStringSetting = "SqlConnectionString")]
        public static Product Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addproduct-params")]
            HttpRequestData req)
        {
            if (req != null)
            {
                var product = new Product()
                {
                    Name = HttpUtility.ParseQueryString(req.Url.Query)["name"],
                    ProductID = int.Parse(HttpUtility.ParseQueryString(req.Url.Query)["productId"], null),
                    Cost = int.Parse(HttpUtility.ParseQueryString(req.Url.Query)["cost"], null)
                };
                return product;
            }
            return null;
        }
    }
}
