// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

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
        private readonly IReadOnlyDictionary<string, Type> _emptyBindingContract = new Dictionary<string, Type>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerBinding"/> class.
        /// </summary>
        /// <param name="connectionStringSetting"> 
        /// The name of the app setting that stores the SQL connection string
        /// </param>
        /// <param name="table"> 
        /// The name of the user table that changes are being tracked on
        /// </param>
        /// <param name="configuration">
        /// Used to extract the connection string from connectionStringSetting
        /// </param>
        /// <param name="parameter">
        /// The parameter that contains the SqlTriggerAttribute of the user's function
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of the parameters are null
        /// </exception>
        public SqlTriggerBinding(string table, string connectionStringSetting, IConfiguration configuration, ParameterInfo parameter)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _connectionStringSetting = connectionStringSetting ?? throw new ArgumentNullException(nameof(connectionStringSetting));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        }

        public Type TriggerValueType => typeof(ChangeTableData);

        /// <summary>
        /// Returns an empty binding contract. The type that SqlTriggerAttribute is bound to is checked in 
        /// <see cref="SqlTriggerAttributeBindingProvider.TryCreateAsync(TriggerBindingProviderContext)"/>
        /// </summary>
        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _emptyBindingContract; }
        }

        /// <summary>
        /// Binds the <see cref="ChangeTableData"/> represented by "value" with a <see cref="SqlValueBinder"/> which converts it to an IEnumerable<SqlChangeTrackingEntry<T>>
        /// </summary>
        /// <param name="value">
        /// The <see cref="ChangeTableData"/> data, which contains a list of rows from the worker/change tables as well as information used to build up queries to
        /// get the associated information from the user table
        /// </param>
        /// <param name="context">
        /// Unused
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if "value" is not of type ChangeTableData
        /// </exception>
        /// <returns>
        /// The ITriggerData which stores the ChangeTableData as well as the SqlValueBinder which converts it to the form eventually passed to the user's function
        /// </returns>
        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            var changeData = value as ChangeTableData;

            if (changeData ==  null)
            {
                throw new InvalidOperationException("The value passed to the SqlTrigger BindAsync must be of type ChangeTableData");
            }

            var bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            bindingData.Add("SqlTrigger", changeData);

            return Task.FromResult<ITriggerData>(new TriggerData(new SqlValueBinder(_parameter, changeData, _table, _connectionStringSetting, _configuration), bindingData));
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

            return Task.FromResult<IListener>(new SqlTriggerListener(_table, _connectionStringSetting, _configuration, context.Executor));
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
        /// Responsible for converting the ChangeTableData passed by the function executor into an IEnumerable<SqlChangeTrackingEntry<T>>,
        /// where T is a user-specified POCO representing rows from the monitored table
        /// </summary>
        private class SqlValueBinder : IValueProvider
        {
            private readonly ParameterInfo _parameter;
            private ChangeTableData _changeData;
            private readonly SqlChangeTrackingConverter _converter;
            private readonly string _table;

            /// <summary>
            /// Initializes a new instance of the <see cref="SqlValueBinder"/> class.
            /// </summary>
            /// <param name="connectionStringSetting"> 
            /// The name of the app setting that stores the SQL connection string
            /// </param>
            /// <param name="table"> 
            /// The name of the user table that changes are being tracked on
            /// </param>
            /// <param name="configuration">
            /// Used to extract the connection string from connectionStringSetting
            /// </param>
            /// <param name="parameter">
            /// The parameter that contains the SqlTriggerAttribute of the user's function
            /// </param>
            /// <param name="changeData">
            /// The <see cref="ChangeTableData"/> which contains rows from the worker/change tables which store information
            /// about changes to "table"
            /// </param>
            /// <exception cref="ArgumentNullException">
            /// Thrown if any of the parameters are null
            /// </exception>
            public SqlValueBinder(ParameterInfo parameter, ChangeTableData changeData, string table, string connectionStringSetting, 
                IConfiguration configuration)
            {
                _table = table ?? throw new ArgumentNullException(nameof(table));
                _parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
                _changeData = changeData ?? throw new ArgumentNullException(nameof(changeData));
                // Will throw null exceptions if connectionStringSetting/configuration are null
                _converter = new SqlChangeTrackingConverter(table, connectionStringSetting, configuration);
            }

            public Type Type => _parameter.ParameterType;

            /// <summary>
            /// Converts the ChangeTableData passed to the constructor into an IEnumerable<SqlChangeTrackingEntry<T>> using the
            /// type information stored in "parameter"
            /// </summary>
            /// <exception cref="InvalidOperationException">
            /// If the type of the trigger binding data stored in parameter is not composed of three types, as IEnumerable<SqlChangeTrackingEntry<T>> is,
            /// although we do not check that the first type is IEnumerable, the second is SqlChangeTrackingEntry, etc.
            /// </exception>
            /// <returns>
            /// The IEnumerable<SqlChangeTrackingEntry<T>> which is eventually passed as the trigger data to the user's function
            /// </returns>
            public async Task<object> GetValueAsync()
            {
                // This shouldn't fail because we already check for valid types in SqlTriggerAttributeBindingProvider
                // This line extracts the type of the POCO
                var type = _parameter.ParameterType.GetGenericArguments()[0].GetGenericArguments()[0];
                var typeOfConverter = _converter.GetType();
                var method = typeOfConverter.GetMethod("BuildSqlChangeTrackingEntries");
                var genericMethod = method.MakeGenericMethod(type);
                var task = (Task<object>) genericMethod.Invoke(_converter, new object[] { _changeData.WorkerTableRows, _changeData.WhereChecks, _changeData.PrimaryKeys});
                return await task;
            }

            /// <returns>The name of the table for which changes are being tracked</returns>
            public string ToInvokeString()
            {
                return _table;
            }
        }
    }
}
