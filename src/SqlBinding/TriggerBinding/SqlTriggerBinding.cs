using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlTriggerBinding : ITriggerBinding
    {
        private readonly string _connectionStringSetting;
        private readonly string _table;
        private readonly IConfiguration _configuration;
        private readonly ParameterInfo _parameter;
        private readonly SqlTriggerAttribute _attribute;
        private readonly BindingDataProvider _bindingDataProvider;
        private readonly IReadOnlyDictionary<string, Type> _contract;

        public SqlTriggerBinding(string table, string connectionStringSetting, IConfiguration configuration, ParameterInfo parameter)
        {
            _table = table;
            _connectionStringSetting = connectionStringSetting;
            _configuration = configuration;
            _parameter = parameter;
            _attribute = parameter.GetCustomAttribute<SqlTriggerAttribute>(inherit: false);
            _bindingDataProvider = BindingDataProvider.FromTemplate(_attribute.CommandText);
            _contract = CreateBindingContract();
        }

        private IReadOnlyDictionary<string, Type> CreateBindingContract()
        {
            var contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            contract.Add("SqlTrigger", typeof(ChangeTableData));

            // Do I even need this? And should follow what HTTP does for adding contract stuff about POCOs both here and in BindAsync
            if (_bindingDataProvider.Contract != null)
            {
                foreach (KeyValuePair<string, Type> item in _bindingDataProvider.Contract)
                {
                    // In case of conflict, binding data from the value type overrides the built-in binding data above.
                    contract[item.Key] = item.Value;
                }
            }

            return contract;
        }

        public Type TriggerValueType => typeof(ChangeTableData);

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _contract; }
        }


        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            var changeData = value as ChangeTableData;

            if (changeData ==  null)
            {
                //Populate this with message
                throw new InvalidOperationException();
            }

            var bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            bindingData.Add("SqlTrigger", changeData);

            return Task.FromResult<ITriggerData>(new TriggerData(new SqlValueBinder(_parameter, changeData, _table, _connectionStringSetting, _configuration), bindingData));
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

        private class SqlValueBinder : IValueProvider
        {
            private readonly ParameterInfo _parameter;
            private ChangeTableData _changeData;
            private readonly SqlChangeTrackingConverter _converter;
            private readonly string _table;

            public SqlValueBinder(ParameterInfo parameter, ChangeTableData changeData, string table, string connectionStringSetting, 
                IConfiguration configuration)
            {
                _table = table;
                _parameter = parameter;
                _changeData = changeData;
                _converter = new SqlChangeTrackingConverter(table, connectionStringSetting, configuration);
            }

            public Type Type => _parameter.ParameterType;

            public async Task<object> GetValueAsync()
            {
                // Should check that the array is populated and all that. Otherwise throw an exception for an invalid type. Is there
                // a way to enforce this somewhere else?
                var type = _parameter.ParameterType.GetGenericArguments()[0].GetGenericArguments();
                var typeOfConverter = _converter.GetType();
                var method = typeOfConverter.GetMethod("BuildSqlChangeTrackingEntries");
                var genericMethod = method.MakeGenericMethod(type);
                var task = (Task<object>) genericMethod.Invoke(_converter, new object[] { _changeData.workerTableRows, _changeData.whereChecks});
                return await task;
            }

            public string ToInvokeString()
            {
                return _table;
            }
        }
    }
}
