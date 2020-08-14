// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerAttributeBindingProvider"/> class.
        /// </summary>
        /// <param name="configuration">
        /// Used to extract the connection string from connectionStringSetting
        /// </param>
        /// <param name="loggerFactory">
        /// Used to create a logger for the SQL trigger binding
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either parameter is null
        /// </exception>
        public SqlTriggerAttributeBindingProvider(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }
            _logger = loggerFactory.CreateLogger(LogCategories.CreateTriggerCategory("Sql"));
        }

        /// <summary>
        /// Creates a SqlTriggerBinding using the information provided in "context"
        /// </summary>
        /// <param name="context">
        /// Contains the SqlTriggerAttribute used to build up a SqlTriggerBinding
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if context is null
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// If the SqlTriggerAttribute is bound to an invalid Type. Currently only IEnumerable<SqlChangeTrackingEntry<T>> 
        /// is supported, where T is a user-defined POCO representing a row of their table
        /// </exception>
        /// <returns>
        /// Null if "context" does not contain a SqlTriggerAttribute. Otherwise returns a SqlTriggerBinding{T} associated
        /// with the SqlTriggerAttribute in "context", where T is the user-defined POCO
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
                throw new InvalidOperationException($"Can't bind SqlTriggerAttribute to type {parameter.ParameterType}. Only IEnumerable<SqlChangeTrackingEntry<T>>" +
                    $" is supported, where T is a user-defined POCO that matches the schema of the tracked table");
            }

            Type type = parameter.ParameterType.GetGenericArguments()[0].GetGenericArguments()[0];
            Type typeOfTriggerBinding = typeof(SqlTriggerBinding<>).MakeGenericType(type);
            ConstructorInfo constructor = typeOfTriggerBinding.GetConstructor(new Type[] { typeof(string), typeof(string), typeof(ParameterInfo), typeof(ILogger) });
            return Task.FromResult<ITriggerBinding>((ITriggerBinding)constructor.Invoke(new object[] {attribute.TableName,
                SqlBindingUtilities.GetConnectionString(attribute.ConnectionStringSetting, _configuration), parameter, _logger }));
        }

        /// <summary>
        /// Determines if type is a valid Type, Currently only IEnumerable<SqlChangeTrackingEntry<T>> is supported, where
        /// T is a user-defined POCO representing a row of their table
        /// </summary>
        /// <param name="type"></param>
        /// <returns>True is type is a valid Type, otherwise false</returns>
        private static bool IsValidType(Type type)
        {
            Type[] genericArguments = type.GetGenericArguments();
            if (genericArguments.Length != 1)
            {
                return false;
            }
            return (typeof(IEnumerable).IsAssignableFrom(type)) && genericArguments[0].GetGenericTypeDefinition().Equals(typeof(SqlChangeTrackingEntry<>));
        }
    }
}