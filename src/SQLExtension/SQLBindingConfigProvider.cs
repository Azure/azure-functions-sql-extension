using System;
using System.Data.SqlClient;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
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
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context is null");
            }
            var rule = context.AddBindingRule<SQLBindingAttribute>();
            var converter = new SQLConverter();
            rule.BindToInput<SqlCommand>(converter);
            rule.BindToInput<OpenType>(typeof(SQLGenericsConverter<>));
        }
    }
}