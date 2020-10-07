using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            [TimerTrigger("0 */1 * * * *")]TimerInfo myTimer, ILogger log,
            [Sql("Products", ConnectionStringSetting = "SqlConnectionString")] ICollector<Product> products)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            log.LogInformation($"{DateTime.Now} starting execution #{_executionNumber}");
            int totalUpserts = 100000;
            
            List<Product> newProducts = GetNewProducts(totalUpserts, _executionNumber * 100);
            foreach (var product in newProducts)
            {
                products.Add(product);
            }

            sw.Stop();


            string line = $"{DateTime.Now} finished execution #{_executionNumber}. Total time to create rows={sw.ElapsedMilliseconds}.";

            log.LogInformation(line);
            _executionNumber++;
        }
    }
}
