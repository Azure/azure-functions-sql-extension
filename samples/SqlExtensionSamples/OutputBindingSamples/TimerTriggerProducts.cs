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

        /// <summary>
        /// This timer function runs every 30 seconds. Each time it generates 1000 rows of data, starting at a ProductID of 10000.
        /// </summary>
        [FunctionName("TimerTriggerProducts")]
        public static void Run(
            [TimerTrigger("*/30 * * * * *")] TimerInfo req, ILogger log,
            [Sql("Products", ConnectionStringSetting = "SqlConnectionString")] ICollector<Product> products)
        {
            int totalUpserts = 1000;
            int productIdStart = 10000;
            log.LogInformation($"{DateTime.Now} starting execution #{_executionNumber}. Rows to generate={totalUpserts}.");

            var sw = new Stopwatch();
            sw.Start();

            List<Product> newProducts = GetNewProducts(totalUpserts, (_executionNumber * totalUpserts) + productIdStart);
            foreach (Product product in newProducts)
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
