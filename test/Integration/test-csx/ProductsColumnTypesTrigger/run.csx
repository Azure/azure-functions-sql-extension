#load "../Common/Product.csx"
#r "Newtonsoft.Json"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.Sql;

public static void Run(IReadOnlyList<SqlChange<ProductColumnTypes>> changes, ILogger log)
{
    log.LogInformation("SQL Changes: " + Microsoft.Azure.WebJobs.Extensions.Sql.Utils.JsonSerializeObject(changes));
}