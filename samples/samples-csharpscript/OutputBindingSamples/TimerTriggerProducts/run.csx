#load "../../Common/product.csx"
#r "Newtonsoft.Json"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

private static int _executionNumber = 0;
public static void Run(TimerInfo myTimer, ILogger log,
            [Sql("Products", "SqlConnectionString")] ICollector<Product> products)
{
    int totalUpserts = 1000;
    log.LogInformation($"{DateTime.Now} starting execution #{_executionNumber}. Rows to generate={totalUpserts}.");

    var sw = new Stopwatch();
    sw.Start();

    List<Product> newProducts = ProductUtilities.GetNewProducts(totalUpserts);
    foreach (Product product in newProducts)
    {
        products.Add(product);
    }

    sw.Stop();

    string line = $"{DateTime.Now} finished execution #{_executionNumber}. Total time to create {totalUpserts} rows={sw.ElapsedMilliseconds}.";
    log.LogInformation(line);

    _executionNumber++;
}
