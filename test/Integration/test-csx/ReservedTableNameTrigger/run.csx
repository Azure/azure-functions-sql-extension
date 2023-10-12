#r "Newtonsoft.Json"
#r "Microsoft.Azure.WebJobs.Extensions.Sql"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.Sql;

public static void Run(IReadOnlyList<SqlChange<User>> changes, ILogger log)
{
    // The output is used to inspect the trigger binding parameter in test methods.
    log.LogInformation("SQL Changes: " + Microsoft.Azure.WebJobs.Extensions.Sql.Utils.JsonSerializeObject(changes));
}

public class User
{
    public string UserName { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is User)
        {
            var that = obj as User;
            return this.UserId == that.UserId && this.UserName == that.UserName && this.FullName == that.FullName;
        }

        return false;
    }
}