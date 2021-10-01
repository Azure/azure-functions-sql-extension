// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using static SqlExtensionSamples.ProductUtilities;

namespace SqlExtensionSamples.TriggerBindingSamples
{
    public static class TimerTriggerProducts
    {
        private static int _executionNumber = 0;
        [FunctionName("TimerTriggerProducts")]
        public static void Run(
            [TimerTrigger("0 */3 * * * *")] TimerInfo _, ILogger log,
            [Sql("Products", ConnectionStringSetting = "SqlConnectionString")] ICollector<Product> products)
        {
            int totalUpserts = 1000;
            log.LogInformation($"{DateTime.Now} starting execution #{_executionNumber}. Rows to generate={totalUpserts}.");

            var sw = new Stopwatch();
            sw.Start();

            List<Product> newProducts = GetNewProducts(totalUpserts, _executionNumber * 100);
            foreach (var product in newProducts)
            {
                products.Add(product);
            }

            sw.Stop();

            string line = $"{DateTime.Now} finished execution #{_executionNumber}. Total time to create {totalUpserts} rows={sw.ElapsedMilliseconds}.";
            log.LogInformation(line);

            _executionNumber++;
        }
    }
}
