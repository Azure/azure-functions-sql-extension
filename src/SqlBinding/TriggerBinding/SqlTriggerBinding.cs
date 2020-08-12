// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Represents the SQL trigger binding for a given user table being monitored for changes
    /// </summary>
    /// <typeparam name="T">A user-defined POCO that represents a row of the user's table</typeparam>
    internal class SqlTriggerBinding<T> : ITriggerBinding
    {
        private readonly string _connectionString;
        private readonly string _table;
        private readonly ParameterInfo _parameter;
        private readonly ILogger _logger;
        private static readonly IReadOnlyDictionary<string, Type> _emptyBindingContract = new Dictionary<string, Type>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerBinding<typeparamref name="T"/>"/> class.
        /// </summary>
        /// <param name="connectionString">
        /// The SQL connection string used to connect to the user's database
        /// </param>
        /// <param name="table"> 
        /// The name of the user table that changes are being tracked on
        /// </param>
        /// <param name="parameter">
        /// The parameter that contains the SqlTriggerAttribute of the user's function
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of the parameters are null
        /// </exception>
        public SqlTriggerBinding(string table, string connectionString, ParameterInfo parameter, ILogger logger)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the type of the value the Trigger receives from the Executor.
        /// </summary>
        public Type TriggerValueType => typeof(IEnumerable<SqlChangeTrackingEntry<T>>);

        /// <summary>
        /// Returns an empty binding contract. The type that SqlTriggerAttribute is bound to is checked in 
        /// <see cref="SqlTriggerAttributeBindingProvider.TryCreateAsync(TriggerBindingProviderContext)"/>
        /// </summary>
        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _emptyBindingContract; }
        }

        /// <summary>
        /// Binds the list of <see cref="SqlChangeTrackingEntry<typeparamref name="T"/>"/> represented by "value" with a <see cref="SimpleValueProvider"/>
        /// which (as the name suggests) simply returns "value" 
        /// <param name="value">
        /// The list of <see cref="SqlChangeTrackingEntry<typeparamref name="T"/>"/> data
        /// </param>
        /// <param name="context">
        /// Unused
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if "value" is not of type IEnumerable<SqlChangeTrackingEntry<typeparamref name="T"/>>
        /// </exception>
        /// <returns>
        /// The ITriggerData which stores the list of change tracking entries as well as the SimpleValueBinder
        /// </returns>
        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            var changeData = value as IEnumerable<SqlChangeTrackingEntry<T>>;

            if (changeData == null)
            {
                throw new InvalidOperationException("The value passed to the SqlTrigger BindAsync must be of type IEnumerable<SqlChangeTrackingEntry<T>>");
            }

            var bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            bindingData.Add("SqlTrigger", changeData);

            return Task.FromResult<ITriggerData>(new TriggerData(new SimpleValueProvider(_parameter.ParameterType, changeData, _table), bindingData));
        }

        /// <summary>
        /// Creates a listener that will monitor for changes to the user's table
        /// </summary>
        /// <param name="context">
        /// Context for the listener, including the executor that executes the user's function when changes are detected in the user's table
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if context is null
        /// </exception>
        /// <returns>
        /// The listener
        /// </returns>
        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context", "Missing listener context");
            }
            return Task.FromResult<IListener>(new SqlTriggerListener<T>(_table, _connectionString, context.Executor, _logger));
        }

        /// <returns> A description of the SqlTriggerParameter (<see cref="SqlTriggerParameterDescriptor"/> </returns>
        public ParameterDescriptor ToParameterDescriptor()
        {
            return new SqlTriggerParameterDescriptor
            {
                Name = _parameter.Name,
                Type = "SqlTrigger",
                TableName = _table
            };
        }

        /// <summary>
        /// Simply returns whatever value was passed to it in the constructor without modifying it
        /// </summary>
        internal class SimpleValueProvider : IValueProvider
        {
            private readonly Type _type;
            private readonly object _value;
            private readonly string _invokeString;

            public SimpleValueProvider(Type type, object value, string invokeString)
            {
                _type = type;
                _value = value;
                _invokeString = invokeString;
            }

            /// <summary>
            /// Returns the type that the trigger binding is bound to (IEnumerable<SqlChangeTrackingEntry<typeparamref name="T"/>>)
            /// </summary>
            public Type Type => _type;

            public Task<object> GetValueAsync()
            {
                return Task.FromResult(_value);
            }

            /// <summary>
            /// Returns the table name that changes are being tracked on
            /// </summary>
            /// <returns></returns>
            public string ToInvokeString()
            {
                return _invokeString;
            }
        }
    }
}