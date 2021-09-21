using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using static SqlExtensionSamples.ProductUtilities;

namespace SqlExtensionSamples.OutputBindingSamples
{
    public static class QueueTriggerProducts
    {
        [FunctionName("QueueTriggerProducts")]
        public static void Run(
            [QueueTrigger("testqueue")] string queueMessage, ILogger log,
            [Sql("[dbo].[Products]", ConnectionStringSetting = "SqlConnectionString")] ICollector<Product> products)
        {
            int totalUpserts = 100;
            log.LogInformation($"[QueueTrigger]: {DateTime.Now} starting execution {queueMessage}. Rows to generate={totalUpserts}.");

            Stopwatch sw = new Stopwatch();
            sw.Start();

            List<Product> newProducts = GetNewProductsRandomized(totalUpserts, 2 * 100);
            foreach (var product in newProducts)
            {
                products.Add(product);
            }

            string line = $"[QueueTrigger]: {DateTime.Now} finished execution {queueMessage}. Total time to create {totalUpserts} rows={sw.ElapsedMilliseconds}.";
            log.LogInformation(line);

        }
    }
}
