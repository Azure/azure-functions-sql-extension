#load "../Common/Product.csx"
#load "../Common/utils.csx"
#r "Newtonsoft.Json"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.Sql;

public static void Run(IReadOnlyList<SqlChange<Product>> changes, ILogger log)
{
    log.LogInformation("Trigger2 Changes: " + Microsoft.Azure.WebJobs.Extensions.Sql.Utils.JsonSerializeObject(changes));
}