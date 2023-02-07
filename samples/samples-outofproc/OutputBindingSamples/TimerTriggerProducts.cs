// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.OutputBindingSamples
{
    public static class TimerTriggerProducts
    {
        /// <summary>
        /// This timer function runs evyery 5 seconds, each time it upserts 1000 rows of data.
        /// </summary>
        [Function("TimerTriggerProducts")]
        [SqlOutput("Products", "SqlConnectionString")]
        public static List<Product> Run(
            [TimerTrigger("*/5 * * * * *")] TimerInfo req, FunctionContext context)
        {
            int totalUpserts = 1000;

            List<Product> newProducts = ProductUtilities.GetNewProducts(totalUpserts);

            return newProducts;
        }
    }
}
