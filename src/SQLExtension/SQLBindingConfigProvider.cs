using System;
using System.Data.SqlClient;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using SQLBindingExtension;
using static SQLBindingExtension.SQLCollectorBuilders;
using static SQLBindingExtension.SQLConverters;

namespace Microsoft.Azure.WebJobs.Extensions.SQL
{
    /// <summary>
    /// Attempts to establish a connection to the SQL server and database specified in the ConnectionString and run the query 
    /// specified in SQLQuery <see cref="SQLBindingAttribute"/>
    /// </summary>
    [Extension("SQLBinding")]
    public class SQLBindingConfigProvider : IExtensionConfigProvider
    {
        /// <summary>
        /// Initializes the SQL binding rules
        /// </summary>
        /// <param name="context"> The config context </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if context is null
        /// </exception>
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context is null");
            }
            var rule = context.AddBindingRule<SQLBindingAttribute>();
            var converter = new SQLConverter();
            rule.BindToInput<SqlCommand>(converter);
            rule.BindToCollector<OpenType>(typeof(SQLAsyncCollectorBuilder<>));
            rule.BindToCollector<OpenType>(typeof(SQLCollectorBuilder<>));
            rule.BindToInput<OpenType>(typeof(SQLGenericsConverter<>));
        }
    }
}