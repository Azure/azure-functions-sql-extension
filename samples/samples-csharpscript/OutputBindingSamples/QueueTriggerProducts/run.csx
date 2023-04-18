#load "../../Common/product.csx"
#r "Newtonsoft.Json"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

public static void Run(string queueMessage, ILogger log, ICollector<Product> products)
{
    int totalUpserts = 100;
    log.LogInformation($"[QueueTrigger]: {DateTime.Now} starting execution {queueMessage}. Rows to generate={totalUpserts}.");

    var sw = new Stopwatch();
    sw.Start();

    List<Product> newProducts = ProductUtilities.GetNewProducts(totalUpserts);
    foreach (Product product in newProducts)
    {
        products.Add(product);
    }

    string line = $"[QueueTrigger]: {DateTime.Now} finished execution {queueMessage}. Total time to create {totalUpserts} rows={sw.ElapsedMilliseconds}.";
    log.LogInformation(line);
}
