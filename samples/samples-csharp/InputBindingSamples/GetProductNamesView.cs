// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.InputBindingSamples
{
    /// <summary>
    /// This shows an example of a SQL Input binding that queries from a SQL View named ProductNames.
    /// </summary>
    public static class GetProductNamesView
    {
        [FunctionName("GetProductNamesView")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getproduct-namesview/")]
            HttpRequest req,
            [Sql("SELECT * FROM ProductNames",
                "SqlConnectionString")]
            IEnumerable<ProductName> products)
        {
            return new OkObjectResult(products);
        }
    }
}
