// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Provider class for SQL trigger parameter binding.
    /// </summary>
    internal sealed class SqlTriggerBindingProvider : ITriggerBindingProvider
    {
        private readonly IConfiguration _configuration;
        private readonly IHostIdProvider _hostIdProvider;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerBindingProvider"/> class.
        /// </summary>
        /// <param name="configuration">Configuration to retrieve settings from</param>
        /// <param name="hostIdProvider">Provider of unique host identifier</param>
        /// <param name="loggerFactory">Used to create logger instance</param>
        public SqlTriggerBindingProvider(IConfiguration configuration, IHostIdProvider hostIdProvider, ILoggerFactory loggerFactory)
        {
            this._configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this._hostIdProvider = hostIdProvider ?? throw new ArgumentNullException(nameof(hostIdProvider));

            _ = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            this._logger = loggerFactory.CreateLogger(LogCategories.CreateTriggerCategory("Sql"));
        }

        /// <summary>
        /// Creates SQL trigger parameter binding.
        /// </summary>
        /// <param name="context">Contains information about trigger parameter and trigger attributes</param>
        /// <exception cref="ArgumentNullException">Thrown if the context is null</exception>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="SqlTriggerAttribute" /> is bound to an invalid parameter type.</exception>
        /// <returns>
        /// Null if the user function parameter does not have <see cref="SqlTriggerAttribute" /> applied. Otherwise returns an
        /// <see cref="SqlTriggerBinding{T}" /> instance, where T is the user-defined POCO type.
        /// </returns>
        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ParameterInfo parameter = context.Parameter;
            SqlTriggerAttribute attribute = parameter.GetCustomAttribute<SqlTriggerAttribute>(inherit: false);

            // During application startup, the WebJobs SDK calls 'TryCreateAsync' method of all registered trigger
            // binding providers in sequence for each parameter in the user function. A provider that finds the
            // parameter-attribute that it can handle returns the binding object. Rest of the providers are supposed to
            // return null. This binding object later gets used for binding before every function invocation.
            if (attribute == null)
            {
                return Task.FromResult(default(ITriggerBinding));
            }

            if (!IsValidTriggerParameterType(parameter.ParameterType))
            {
                throw new InvalidOperationException($"Can't bind SqlTriggerAttribute to type {parameter.ParameterType}." +
                    " Only IReadOnlyList<SqlChange<T>> is supported, where T is the type of user-defined POCO that" +
                    " matches the schema of the user table");
            }

            string connectionString = SqlBindingUtilities.GetConnectionString(attribute.ConnectionStringSetting, this._configuration);

            // Extract the POCO type 'T' and use it to instantiate class 'SqlTriggerBinding<T>'.
            Type userType = parameter.ParameterType.GetGenericArguments()[0].GetGenericArguments()[0];
            Type bindingType = typeof(SqlTriggerBinding<>).MakeGenericType(userType);

            var constructorParameterTypes = new Type[] { typeof(string), typeof(string), typeof(ParameterInfo), typeof(IHostIdProvider), typeof(ILogger) };
            ConstructorInfo bindingConstructor = bindingType.GetConstructor(constructorParameterTypes);

            object[] constructorParameterValues = new object[] { connectionString, attribute.TableName, parameter, this._hostIdProvider, this._logger };
            var triggerBinding = (ITriggerBinding)bindingConstructor.Invoke(constructorParameterValues);

            return Task.FromResult(triggerBinding);
        }

        /// <summary>
        /// Checks if the type of trigger parameter in the user function is of form <see cref="IReadOnlyList<SqlChange{T}>" />.
        /// </summary>
        private static bool IsValidTriggerParameterType(Type type)
        {
            return
                type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) &&
                type.GetGenericArguments()[0].IsGenericType &&
                type.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(SqlChange<>);
        }
    }
}