// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker.Extension.Sql;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc
{
    public static class AddProduct
    {
        [Function("AddProductOutOfProc")]
        [SqlOutput("dbo.Products", ConnectionStringSetting = "SqlConnectionString")]
        public static Product Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addproductoutofproc")]
            [FromBody] Product prod)
        {
            return prod;
        }
    }
}
