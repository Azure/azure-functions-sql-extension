#load "../Common/Product.csx"
#r "Newtonsoft.Json"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.Sql;

public static void Run(IReadOnlyList<SqlChange<Product>> changes, ILogger log)
{
    string expectedMaxBatchSize = Environment.GetEnvironmentVariable("TEST_EXPECTED_MAX_BATCH_SIZE");
    if (!string.IsNullOrEmpty(expectedMaxBatchSize) && int.Parse(expectedMaxBatchSize) != changes.Count)
    {
        throw new Exception($"Invalid max batch size, got {changes.Count} changes but expected {expectedMaxBatchSize}");
    }
    // The output is used to inspect the trigger binding parameter in test methods.
    log.LogInformation("SQL Changes: " + Utils.JsonSerializeObject(changes));
}