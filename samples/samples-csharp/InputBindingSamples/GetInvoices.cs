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
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getinvoices")]
            HttpRequest req,
            [Sql("SELECT * FROM Invoices",
                CommandType = System.Data.CommandType.Text,
                ConnectionStringSetting = "SqlConnectionString")]
            IEnumerable<Invoice> invoices)
        {
            return new OkObjectResult(invoices);
        }
    }
}
