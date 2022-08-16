// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples
{
    public static class AddInvoicesArray
    {
        [FunctionName("AddInvoicesArray")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "addinvoices-array")]
            [FromBody] List<Invoice> invoices,
            [Sql("dbo.Invoices", ConnectionStringSetting = "SqlConnectionString")] out Invoice[] output)
        {
            // Upsert the invoices, which will insert them into the Invoice table if the primary key (InvoiceID) for that item doesn't exist. 
            // If it does then update it.
            output = invoices.ToArray();
            return new CreatedResult($"/api/addinvoices-array", output);
        }
    }
}
