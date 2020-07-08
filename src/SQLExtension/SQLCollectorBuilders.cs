using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.SQL;
using static SQLBindingExtension.SQLCollectors;

namespace SQLBindingExtension
{
    internal class SQLCollectorBuilders
    {
        internal class SQLAsyncCollectorBuilder<T> : IConverter<SQLBindingAttribute, IAsyncCollector<T>>
        {
            public IAsyncCollector<T> Convert(SQLBindingAttribute attribute)
            {
                return new SQLAsyncCollector<T>(SQLConverters.BuildConnection(null, attribute), attribute);
            }
        }

        internal class SQLCollectorBuilder<T> : IConverter<SQLBindingAttribute, ICollector<T>>
        {
            public ICollector<T> Convert(SQLBindingAttribute attribute)
            {
                return new SQLCollector<T>(SQLConverters.BuildConnection(null, attribute), attribute);
            }
        }
    }
}
