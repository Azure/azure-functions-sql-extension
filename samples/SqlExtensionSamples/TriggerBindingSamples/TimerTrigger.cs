using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using static SqlExtensionSamples.ProductUtilities;

namespace SqlExtensionSamples.TriggerBindingSamples
{
    public static class TimerTrigger
    {
        private static int _executionNumber = 0;
        [FunctionName("TimerTrigger")]
        public static void Run(
            [TimerTrigger("0 */3 * * * *")]TimerInfo myTimer, ILogger log,
            [Sql("Products", ConnectionStringSetting = "SqlConnectionString")] ICollector<Product> products)
        {
            List<Product> newProducts = GetNewProducts(100, _executionNumber);
            foreach (var product in newProducts)
            {
                products.Add(product);
            }
            _executionNumber++;
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now} for the {_executionNumber} time");
        }
    }
}
