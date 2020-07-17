using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Sql;
using Microsoft.Azure.WebJobs.Hosting;


[assembly: WebJobsStartup(typeof(SqlBindingStartup))]
namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    public class SqlBindingStartup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            builder.AddSqlBinding();
        }
    }
}