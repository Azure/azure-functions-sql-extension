using SQLBinding;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;


[assembly: WebJobsStartup(typeof(SQLBindingStartup))]
namespace SQLBinding
{
    public class SQLBindingStartup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            builder.AddSQLBinding();
        }
    }
}