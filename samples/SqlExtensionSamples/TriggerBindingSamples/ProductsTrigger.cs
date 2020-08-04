using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
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
            enumerator.MoveNext();
            Console.WriteLine(enumerator.Current.ChangeType);
            // Products is a JSON representation of the returned rows. For example, if there are two returned rows,
            // products could look like:
            // [{"ProductID":1,"Name":"Dress","Cost":100},{"ProductID":2,"Name":"Skirt","Cost":100}]
        }
    }
}
