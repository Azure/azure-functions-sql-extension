using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlTriggerBinding : ITriggerBinding
    {
        private readonly string _connectionStringSetting;
        private readonly string _table;
        private readonly IConfiguration _configuration;
        private readonly ParameterInfo _parameter;
        private readonly IReadOnlyDictionary<string, Type> _emptyBindingContract = new Dictionary<string, Type>();
        private readonly IReadOnlyDictionary<string, object> _emptyBindingData = new Dictionary<string, object>();

        public SqlTriggerBinding(string table, string connectionStringSetting, IConfiguration configuration, ParameterInfo parameter)
        {
            _table = table;
            _connectionStringSetting = connectionStringSetting;
            _configuration = configuration;
            _parameter = parameter;
        }

        public Type TriggerValueType => typeof(IEnumerable<SqlChangeTrackingEntry>);

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _emptyBindingContract; }
        }

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            // ValueProvider is via binding rules. 
            return Task.FromResult<ITriggerData>(new TriggerData(null, _emptyBindingData));
        }

        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context", "Missing listener context");
            }

            return Task.FromResult<IListener>(new SqlTriggerListener(_table, _connectionStringSetting, _configuration, context.Executor));
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new SqlTriggerParameterDescriptor
            {
                Name = _parameter.Name,
                Type = "SqlTrigger",
                TableName = _table
            };
        }
    }
}
