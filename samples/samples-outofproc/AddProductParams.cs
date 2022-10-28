// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker.Extension.Sql;
using Microsoft.Azure.Functions.Worker;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc
{
    public static class AddProductParams
    {
        [Function("AddProductParams")]
        [SqlOutput("dbo.Products", ConnectionStringSetting = "SqlConnectionString")]
        public static Product Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addproduct-params")]
            HttpRequest req)
        {
            if (req != null)
            {
                var product = new Product()
                {
                    Name = req.Query["name"],
                    ProductID = int.Parse(req.Query["productId"], null),
                    Cost = int.Parse(req.Query["cost"], null)
                };
                return product;
            }
            return null;
        }
    }
}
