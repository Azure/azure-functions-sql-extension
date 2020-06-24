using System;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using SQLBindingExtension;

namespace Microsoft.Azure.WebJobs.Extensions.SQL
{
    /// <summary>
    /// Attempts to establish a connection to the SQL server and database specified in the ConnectionString and run the query 
    /// specified in SQLQuery <see cref="SQLBindingAttribute"/>
    /// </summary>
    [Extension("SQLBinding")]
    public class SQLBindingConfigProvider : IExtensionConfigProvider
    {
        private SqlConnectionWrapper _connection;
        public SQLBindingConfigProvider(SqlConnectionWrapper connection)
        {
            _connection = connection;
        }
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context is null");
            }
            var rule = context.AddBindingRule<SQLBindingAttribute>();
            rule.BindToInput<OpenType>(typeof(SQLGenericsConverter<>), _connection);
        }
    }
}