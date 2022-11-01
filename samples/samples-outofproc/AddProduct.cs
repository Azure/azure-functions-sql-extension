// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker.Extension.Sql;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.IO;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc
{
    public static class AddProduct
    {
        [Function("AddProduct")]
        [SqlOutput("dbo.Products", ConnectionStringSetting = "SqlConnectionString")]
        public static Product Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addproduct")]
            HttpRequestData req)
        {
            using var bodyReader = new StreamReader(req.Body);
            Product prod = JsonConvert.DeserializeObject<Product>(bodyReader.ReadToEnd());
            return prod;
        }
    }
}
