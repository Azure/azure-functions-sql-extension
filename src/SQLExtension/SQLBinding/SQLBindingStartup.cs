using Microsoft.Azure.WebJobs.Extensions.SQL;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;


[assembly: WebJobsStartup(typeof(SQLBindingStartup))]
namespace Microsoft.Azure.WebJobs.Extensions.SQL
{
    public class SQLBindingStartup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            builder.AddSQLBinding();
        }
    }
}