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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Represents the SQL trigger parameter binding.
    /// </summary>
    /// <typeparam name="T">POCO class representing the row in the user table</typeparam>
    internal sealed class SqlTriggerBinding<T> : ITriggerBinding
    {
        private readonly string _connectionString;
        private readonly string _tableName;
        private readonly string _leasesTableName;
        private readonly ParameterInfo _parameter;
        private readonly IHostIdProvider _hostIdProvider;
        private readonly SqlOptions _sqlOptions;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        private static readonly IReadOnlyDictionary<string, Type> _emptyBindingContract = new Dictionary<string, Type>();
        private static readonly IReadOnlyDictionary<string, object> _emptyBindingData = new Dictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerBinding{T}"/> class.
        /// </summary>
        /// <param name="connectionString">SQL connection string used to connect to user database</param>
        /// <param name="tableName">Name of the user table</param>
        /// <param name="leasesTableName">Optional - Name of the leases table</param>
        /// <param name="parameter">Trigger binding parameter information</param>
        /// <param name="hostIdProvider">Provider of unique host identifier</param>
        /// <param name="sqlOptions"></param>
        /// <param name="logger">Facilitates logging of messages</param>
        /// <param name="configuration">Provides configuration values</param>
        public SqlTriggerBinding(string connectionString, string tableName, string leasesTableName, ParameterInfo parameter, IOptions<SqlOptions> sqlOptions, IHostIdProvider hostIdProvider, ILogger logger, IConfiguration configuration)
        {
            this._connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            this._tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
            this._leasesTableName = leasesTableName;
            this._parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            this._hostIdProvider = hostIdProvider ?? throw new ArgumentNullException(nameof(hostIdProvider));
            this._sqlOptions = (sqlOptions ?? throw new ArgumentNullException(nameof(sqlOptions))).Value;
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Returns the type of trigger value that <see cref="SqlTriggerBinding{T}" /> binds to.
        /// </summary>
        public Type TriggerValueType => typeof(IReadOnlyList<SqlChange<T>>);

        public IReadOnlyDictionary<string, Type> BindingDataContract => _emptyBindingContract;

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            IValueProvider valueProvider = new SqlTriggerValueProvider(this._parameter.ParameterType, value, this._tableName);
            return Task.FromResult<ITriggerData>(new TriggerData(valueProvider, _emptyBindingData));
        }

        public async Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context), "Missing listener context");

            string userFunctionId = this.GetUserFunctionIdAsync();
            string oldUserFunctionId = await this.GetOldUserFunctionIdAsync();
            return new SqlTriggerListener<T>(this._connectionString, this._tableName, this._leasesTableName, userFunctionId, oldUserFunctionId, context.Executor, this._sqlOptions, this._logger, this._configuration);
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new SqlTriggerParameterDescriptor
            {
                Name = this._parameter.Name,
                Type = "SqlTrigger",
                TableName = this._tableName,
            };
        }

        /// <summary>
        /// Returns an ID that uniquely identifies the user function.
        ///
        /// We call the WEBSITE_SITE_NAME from the configuration and use that to create the hash of the
        /// user function id. Appending another hash of class+method in here ensures that if there
        /// are multiple user functions within the same process and tracking the same SQL table, then each one of them
        /// gets a separate view of the table changes.
        /// </summary>
        private string GetUserFunctionIdAsync()
        {
            // Using read-only App name for the hash https://learn.microsoft.com/en-us/azure/app-service/reference-app-settings?tabs=kudu%2Cdotnet#app-environment
            string websiteName = SqlBindingUtilities.GetWebSiteName(this._configuration);

            var methodInfo = (MethodInfo)this._parameter.Member;
            string functionName = $"{methodInfo.DeclaringType.FullName}.{methodInfo.Name}";

            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(websiteName + functionName));
                return new Guid(hash.Take(16).ToArray()).ToString("N").Substring(0, 16);
            }
        }

        /// <summary>
        /// Returns the deprecated ID that was used to identify the user function.
        ///
        /// We call the WebJobs SDK library method to generate the host ID. The host ID is essentially a hash of the
        /// assembly name containing the user function(s). This ensures that if the user ever updates their application,
        /// unless the assembly name is modified, the new application version will be able to resume from the point
        /// where the previous version had left. Appending another hash of class+method in here ensures that if there
        /// are multiple user functions within the same process and tracking the same SQL table, then each one of them
        /// gets a separate view of the table changes.
        /// </summary>
        private async Task<string> GetOldUserFunctionIdAsync()
        {
            string hostId = await this._hostIdProvider.GetHostIdAsync(CancellationToken.None);

            var methodInfo = (MethodInfo)this._parameter.Member;
            string functionName = $"{methodInfo.DeclaringType.FullName}.{methodInfo.Name}";

            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(hostId + functionName));
                return new Guid(hash.Take(16).ToArray()).ToString("N").Substring(0, 16);
            }
        }

    }
}