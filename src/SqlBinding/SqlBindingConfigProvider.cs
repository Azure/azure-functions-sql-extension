using System;
using System.Data.SqlClient;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Configuration;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlConverters;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Attempts to establish a connection to the SQL server and database specified in the ConnectionStringSetting and run the query 
    /// specified in SQLQuery <see cref="SqlAttribute"/>
    /// </summary>
    [Extension("SQLBinding")]
    public class SqlBindingConfigProvider : IExtensionConfigProvider
    {
        private readonly IConfiguration _configuration;
        public SqlBindingConfigProvider(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }
            _configuration = configuration;
        }

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
                throw new ArgumentNullException("context");
            }
            var rule = context.AddBindingRule<SqlAttribute>();
            var converter = new SqlConverter(_configuration);
            rule.BindToInput<SqlCommand>(converter);
            rule.BindToInput<string>(typeof(SqlGenericsConverter<>), _configuration);
            rule.BindToInput<OpenType>(typeof(SqlGenericsConverter<>), _configuration);
            rule.BindToCollector<OpenType>(typeof(SqlAsyncCollectorBuilder<>), _configuration); 
        }
    }
}