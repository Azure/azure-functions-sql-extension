// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker.Extension.Sql;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc
{
    public static class AddProduct
    {
        [Function("AddProduct")]
        [SqlOutput("dbo.Products", ConnectionStringSetting = "SqlConnectionString")]
        public static async Task<Product> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addproduct")]
            HttpRequestData req)
        {
            Product prod = await req.ReadFromJsonAsync<Product>();
            return prod;
        }
    }
}
