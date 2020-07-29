using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Triggers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
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
                return null;
            }

            return new SqlTriggerBinding();
        }
    }
}
