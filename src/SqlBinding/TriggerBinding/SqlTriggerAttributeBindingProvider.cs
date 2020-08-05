// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerAttributeBindingProvider"/> class.
        /// </summary>
        /// <param name="configuration">
        /// Used to extract the connection string from connectionStringSetting
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if configuration is null
        /// </exception>
        public SqlTriggerAttributeBindingProvider(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Creates a SqlTriggerBinding using the information provided in "context"
        /// </summary>
        /// <param name="context">
        /// Contains the SqlTriggerAttribute used to build up a SqlTriggerBinding
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if conctext is null
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// If the SqlTriggerAttribute is bound to an invalid Type. Currently only IEnumerable<SqlChangeTrackingEntry<T>> 
        /// is supported, where T is a user-defined POCO representing a row of their table
        /// </exception>
        /// <returns>
        /// Null if "context" does not contain a SqlTriggerAttribute. Otherwise returns a SqlTriggerBinding associated
        /// with the SqlTriggerAttribute in "context"
        /// </returns>
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

            if (!IsValidType(parameter.ParameterType))
            {
                throw new InvalidOperationException(String.Format("Can't bind SqlTriggerAttribute to type {0}", parameter.ParameterType));
            }

            return Task.FromResult<ITriggerBinding>(new SqlTriggerBinding(attribute.TableName, attribute.ConnectionStringSetting, 
                _configuration, parameter));
        }

        /// <summary>
        /// Determines if type is a valid Type, Currently only IEnumerable<SqlChangeTrackingEntry<T>> is supported, where
        /// T is a user-defined POCO representing a row of their table
        /// </summary>
        /// <param name="type"></param>
        /// <returns>True is type is a valid Type, otherwise false</returns>
        private static bool IsValidType(Type type)
        {
            var genericArguments = type.GetGenericArguments();
            if (genericArguments.Length != 1)
            {
                return false;
            }
            return (typeof(IEnumerable).IsAssignableFrom(type)) && genericArguments[0].GetGenericTypeDefinition().Equals(typeof(SqlChangeTrackingEntry<>));
        }
    }
}
