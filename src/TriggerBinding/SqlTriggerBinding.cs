// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Represents the SQL trigger binding for a given user table being monitored for changes
    /// </summary>
    /// <typeparam name="T">A user-defined POCO that represents a row of the user's table</typeparam>
    internal sealed class SqlTriggerBinding<T> : ITriggerBinding
    {
        private readonly string _connectionString;
        private readonly string _tableName;
        private readonly ParameterInfo _parameter;
        private readonly IHostIdProvider _hostIdProvider;
        private readonly ILogger _logger;
        private static readonly IReadOnlyDictionary<string, Type> _emptyBindingContract = new Dictionary<string, Type>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerBinding{T}"/> class.
        /// </summary>
        /// <param name="tableName">
        /// The name of the user table that changes are being tracked on
        /// </param>
        /// <param name="connectionString">
        /// The SQL connection string used to connect to the user's database
        /// </param>
        /// <param name="parameter">
        /// The parameter that contains the SqlTriggerAttribute of the user's function
        /// </param>
        /// <param name="hostIdProvider">
        /// Used to fetch a unique host identifier
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of the parameters are null
        /// </exception>
        public SqlTriggerBinding(string tableName, string connectionString, ParameterInfo parameter, IHostIdProvider hostIdProvider, ILogger logger)
        {
            this._tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
            this._connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            this._parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            this._hostIdProvider = hostIdProvider ?? throw new ArgumentNullException(nameof(hostIdProvider));
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the type of the value the Trigger receives from the Executor.
        /// </summary>
        public Type TriggerValueType => typeof(IReadOnlyList<SqlChange<T>>);

        /// <summary>
        /// Returns an empty binding contract. The type that SqlTriggerAttribute is bound to is checked in 
        /// <see cref="SqlTriggerAttributeBindingProvider.TryCreateAsync(TriggerBindingProviderContext)"/>
        /// </summary>
        public IReadOnlyDictionary<string, Type> BindingDataContract => _emptyBindingContract;

        /// <summary>
        /// Binds the list of <see cref="SqlChange{T}"/> represented by "value" with a <see cref="SimpleValueProvider"/>
        /// which (as the name suggests) simply returns "value".
        /// <param name="value">
        /// The list of <see cref="SqlChange{T}"/> data.
        /// </param>
        /// <param name="context">
        /// Unused
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if "value" is not of type IReadOnlyList<SqlChange<T>>.
        /// </exception>
        /// <returns>
        /// The ITriggerData which stores the list of SQL table changes as well as the SimpleValueBinder
        /// </returns>
        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            if (!(value is IReadOnlyList<SqlChange<T>> changes))
            {
                throw new InvalidOperationException("The value passed to the SqlTrigger BindAsync must be of type IReadOnlyList<SqlChange<T>>");
            }

            var bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "SqlTrigger", changes }
            };

            return Task.FromResult<ITriggerData>(new TriggerData(new SimpleValueProvider(this._parameter.ParameterType, changes, this._tableName), bindingData));
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
        public async Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), "Missing listener context");
            }

            string userFunctionId = SqlBindingUtilities.AsSingleQuoteEscapedString(await this.GetUserFunctionIdAsync());
            return new SqlTriggerListener<T>(this._connectionString, this._tableName, userFunctionId, context.Executor, this._logger);
        }

        /// <returns> A description of the SqlTriggerParameter (<see cref="SqlTriggerParameterDescriptor"/> </returns>
        public ParameterDescriptor ToParameterDescriptor()
        {
            return new SqlTriggerParameterDescriptor
            {
                Name = this._parameter.Name,
                Type = "SqlTrigger",
                TableName = _tableName
            };
        }

        /// <summary>
        /// Creates a unique ID for user function using host ID and method name.
        ///
        /// We call the WebJobs SDK library method to generate the host ID. The host ID is essentially a hash of the
        /// assembly name containing the user function(s). This ensures that if the user ever updates their application,
        /// unless the assembly name is modified, the new application version will be able to resume from the point
        /// where the previous version had left. Appending another hash of class+method in here ensures that if there
        /// are multiple user functions within the same process and tracking the same SQL table, then each one of them
        /// gets a separate view of the table changes.
        /// </summary>
        private async Task<string> GetUserFunctionIdAsync()
        {
            string hostId = await this._hostIdProvider.GetHostIdAsync(CancellationToken.None);

            var methodInfo = (MethodInfo)this._parameter.Member;
            string functionName = $"{methodInfo.DeclaringType.FullName}.{methodInfo.Name}";

            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(hostId + functionName));

                return SqlBindingUtilities.AsSingleQuoteEscapedString(new Guid(hash.Take(16).ToArray()).ToString("N").Substring(0, 16));
            }
        }

        /// <summary>
        /// Simply returns whatever value was passed to it in the constructor without modifying it
        /// </summary>
        internal class SimpleValueProvider : IValueProvider
        {
            private readonly object _value;
            private readonly string _invokeString;

            public SimpleValueProvider(Type type, object value, string invokeString)
            {
                this.Type = type;
                this._value = value;
                this._invokeString = invokeString;
            }

            /// <summary>
            /// Returns the type that the trigger binding is bound to (IReadOnlyList{SqlChange{T}}"/>>)
            /// </summary>
            public Type Type { get; }

            public Task<object> GetValueAsync()
            {
                return Task.FromResult(this._value);
            }

            /// <summary>
            /// Returns the table name that changes are being tracked on
            /// </summary>
            /// <returns></returns>
            public string ToInvokeString()
            {
                return this._invokeString;
            }
        }
    }
}