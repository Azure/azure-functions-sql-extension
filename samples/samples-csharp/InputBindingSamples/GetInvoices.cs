// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples
{
    public static class GetInvoices
    {
        [FunctionName("GetInvoices")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getinvoices/{count}")]
            HttpRequest req,
            [Sql("SELECT TOP(CAST(@Count AS INT)) * FROM Invoices",
                CommandType = System.Data.CommandType.Text,
                Parameters = "@Count={count}",
                ConnectionStringSetting = "SqlConnectionString")]
            IEnumerable<Invoice> invoices)
        {
            return new OkObjectResult(invoices);
        }
    }
}
