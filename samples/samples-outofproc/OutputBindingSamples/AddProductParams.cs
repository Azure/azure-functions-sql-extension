// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Web;
using System.Collections.Specialized;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.OutputBindingSamples
{
    public static class AddProductParams
    {
        [Function("AddProductParams")]
        [SqlOutput("dbo.Products", "SqlConnectionString")]
        public static Product Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addproduct-params")]
            HttpRequestData req)
        {
            if (req != null)
            {
                NameValueCollection queryStrings = HttpUtility.ParseQueryString(req.Url.Query);
                var product = new Product()
                {
                    Name = queryStrings["name"],
                    ProductId = int.Parse(queryStrings["productId"], null),
                    Cost = int.Parse(queryStrings["cost"], null)
                };
                return product;
            }
            return null;
        }
    }
}
