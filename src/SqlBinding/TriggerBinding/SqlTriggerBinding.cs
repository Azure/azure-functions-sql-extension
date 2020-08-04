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
        private readonly IReadOnlyDictionary<string, Type> _emptyBindingContract = new Dictionary<string, Type>();
        private readonly IReadOnlyDictionary<string, object> _emptyBindingData = new Dictionary<string, object>();

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
            contract.Add("SqlTrigger", typeof(List<Dictionary<string, string>>));

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

        // Change this to WorkerTableRow
        public Type TriggerValueType => typeof(List<Dictionary<string, string>>);

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _contract; }
            //get { return _emptyBindingContract; }
        }


        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            
            var workerTableRows = value as List<Dictionary<string, string>>;

            if (workerTableRows ==  null)
            {
                //Populate this with message
                throw new InvalidOperationException();
            }

            var bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            bindingData.Add("SqlTrigger", workerTableRows);

            return Task.FromResult<ITriggerData>(new TriggerData(new SqlValueBinder(_parameter, workerTableRows, _table, _connectionStringSetting, _configuration), bindingData));
            
            //return Task.FromResult<ITriggerData>(new TriggerData(null, _emptyBindingData));
        }

        public static Task<IValueBinder> GetBinder(SqlTriggerAttribute attribute, Type type)
        {
            throw new NotImplementedException();
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

        private class SqlValueBinder : IValueBinder
        {
            private readonly ParameterInfo _parameter;
            private List<Dictionary<string, string>> _workerTableRows;
            private readonly SqlChangeTrackingConverter _converter;

            public SqlValueBinder(ParameterInfo parameter, List<Dictionary<string, string>> workerTableRows, string table, string connectionStringSetting, 
                IConfiguration configuration)
            {
                _parameter = parameter;
                _workerTableRows = workerTableRows;
                _converter = new SqlChangeTrackingConverter(table, connectionStringSetting, configuration);
            }

            public Type Type => throw new NotImplementedException();

            public Task<object> GetValueAsync()
            {
                Type type = _parameter.ParameterType;
                var nestedTypes = type.GetNestedTypes();
                var typeOfConverter = _converter.GetType();
                var method = typeOfConverter.GetMethod("BuildSqlChangeTrackingEntries");
                var methods = typeOfConverter.GetMethods();
                var genericMethod = method.MakeGenericMethod(type);
                var result = genericMethod.Invoke(_converter, new object[] { _workerTableRows, null });

                return Task.FromResult<object>(_workerTableRows);
            }

            public Task SetValueAsync(object value, CancellationToken cancellationToken)
            {
                var workerTableRows = value as List<Dictionary<string, string>>;

                if (workerTableRows == null)
                {
                    //Populate this with message
                    throw new InvalidOperationException();
                }

                _workerTableRows = workerTableRows;
                return Task.CompletedTask;
            }

            public string ToInvokeString()
            {
                return _workerTableRows.ToString();
            }
        }
    }
}
