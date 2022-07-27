// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;


namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Provider for value that will be passed as argument to the triggered function.
    /// </summary>
    internal class SqlTriggerValueProvider : IValueProvider
    {
        private readonly object _value;
        private readonly string _tableName;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerValueProvider"/> class.
        /// </summary>
        /// <param name="parameterType">Type of the trigger parameter</param>
        /// <param name="value">Value of the trigger parameter</param>
        /// <param name="tableName">Name of the user table</param>
        public SqlTriggerValueProvider(Type parameterType, object value, string tableName)
        {
            this.Type = parameterType;
            this._value = value;
            this._tableName = tableName;
        }

        /// <summary>
        /// Gets the trigger argument value.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Returns value of the trigger argument.
        /// </summary>
        public Task<object> GetValueAsync()
        {
            return Task.FromResult(this._value);
        }

        public string ToInvokeString()
        {
            return this._tableName;
        }
    }
}