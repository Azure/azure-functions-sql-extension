using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.SQL;
using Microsoft.Azure.WebJobs.Host.Config;
using static SQLBindingExtension.SQLCollectors;

namespace SQLBindingExtension
{
    internal class SQLAsyncCollectorBuilder<T> : IConverter<SQLBindingAttribute, IAsyncCollector<T>>
    {
        IAsyncCollector<T> IConverter<SQLBindingAttribute, IAsyncCollector<T>>.Convert(SQLBindingAttribute attribute)
        {
            return new SQLAsyncCollector<T>(SQLConverters.BuildConnection(null, attribute), attribute);
        }
    }
}
