using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlAsyncCollectorBuilder<T> : IConverter<SqlAttribute, IAsyncCollector<T>>
    {
        private readonly IConfiguration _configuration;
        public SqlAsyncCollectorBuilder(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        IAsyncCollector<T> IConverter<SqlAttribute, IAsyncCollector<T>>.Convert(SqlAttribute attribute)
        {
            return new SqlAsyncCollector<T>(SqlConverters.BuildConnection(null, attribute, _configuration), attribute);
        }
    }
}
