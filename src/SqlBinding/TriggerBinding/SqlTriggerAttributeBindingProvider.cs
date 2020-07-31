using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        IConfiguration _configuration;

        public SqlTriggerAttributeBindingProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            SqlTriggerAttribute attribute = parameter.GetCustomAttribute<SqlTriggerAttribute>(inherit: false);

            if (attribute == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            return Task.FromResult<ITriggerBinding>(new SqlTriggerBinding(attribute.CommandText, attribute.ConnectionStringSetting, 
                _configuration, parameter));
        }
    }
}
