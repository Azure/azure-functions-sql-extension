using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Sql;
using System;
using System.Collections.Generic;
using static SqlExtensionSamples.ProductUtilities;

namespace SqlExtensionSamples.TriggerBindingSamples
{
    public static class ProductsTrigger
    {
        [FunctionName("ProductsTrigger")]
        public static void Run(
            [SqlTrigger("Products",
            ConnectionStringSetting = "SQLServerAuthentication")] IEnumerable<SqlChangeTrackingEntry<Product>> changes)
        {
            var enumerator = changes.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var product = enumerator.Current.Data;
                Console.WriteLine(String.Format("ProductID: {0}, Name: {1}, Price: {2}", product.ProductID, product.Name, product.Cost));
            }
        }
    }
}
