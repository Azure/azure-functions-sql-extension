using System;
using System.Data.SqlClient;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using SQLBindingExtension;
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
            // For some reason, this is the only way I could get it to bind to the SQLCollector class in the case that the user's
            // function has as its output binding an ICollector. Otherwise, it would keep binding to the SQLAsyncCollector instead,
            // even if the collector type was an ICollector, not an IAsyncCollector. 
            // The SQLGenericsConverter has two Convert methods, one that creates the SQLCollector and another that creates the 
            // SQLAsyncCollector. The former is called when the user's output binding is an ICollector. The latter is called when 
            // the output binding is an IAsyncCollector. If the output binding is neither, for example a POCO or array of POCOs, then 
            // the SQLAsyncCollectorBuilder is called. This is essentially just a wrapper for a Convert method that creates a 
            // SQLAsyncCollector. 
            rule.BindToInput<OpenType>(typeof(SQLGenericsConverter<>));
            rule.BindToCollector<OpenType>(typeof(SQLAsyncCollectorBuilder<>)); 
        }
    }
}