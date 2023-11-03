// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlScalerProvider : IScaleMonitorProvider, ITargetScalerProvider
    {
        private readonly SqlTriggerScaleMonitor _scaleMonitor;
        private readonly SqlTriggerTargetScaler _targetScaler;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlScalerProvider"/> class.
        /// Use for obtaining scale metrics by scale controller.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="triggerMetadata"></param>
        public SqlScalerProvider(IServiceProvider serviceProvider, TriggerMetadata triggerMetadata)
        {
            IConfiguration config = serviceProvider.GetService<IConfiguration>();
            ILoggerFactory loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            ILogger logger = loggerFactory.CreateLogger(LogCategories.CreateTriggerCategory("Sql"));
            SqlMetaData sqlMetadata = JsonConvert.DeserializeObject<SqlMetaData>(triggerMetadata.Metadata.ToString());
            sqlMetadata.ResolveProperties(serviceProvider.GetService<INameResolver>());
            var userTable = new SqlObject(sqlMetadata.TableName);
            string connectionString = SqlBindingUtilities.GetConnectionString(sqlMetadata.ConnectionStringSetting, config);
            int maxChangesPerWorker = config.GetValue(SqlTriggerConstants.ConfigKey_SqlTrigger_MaxChangesPerWorker, SqlOptions.DefaultMaxChangesPerWorker);
            string userDefinedLeasesTableName = sqlMetadata.LeasesTableName;
            string userFunctionId = (string)triggerMetadata.Properties["userFunctionId"];

            this._scaleMonitor = new SqlTriggerScaleMonitor(userFunctionId, userTable, userDefinedLeasesTableName, connectionString, maxChangesPerWorker, logger);
            this._targetScaler = new SqlTriggerTargetScaler(userFunctionId, userTable, userDefinedLeasesTableName, connectionString, maxChangesPerWorker, logger);
        }

        public IScaleMonitor GetMonitor()
        {
            return this._scaleMonitor;
        }

        public ITargetScaler GetTargetScaler()
        {
            return this._targetScaler;
        }

        internal class SqlMetaData
        {
            [JsonProperty]
            public string ConnectionStringSetting { get; set; }

            [JsonProperty]
            public string TableName { get; set; }

            [JsonProperty]
            public string LeasesTableName { get; set; }

            public void ResolveProperties(INameResolver resolver)
            {
                if (resolver != null)
                {
                    this.TableName = resolver.ResolveWholeString(this.TableName);
                    this.ConnectionStringSetting = resolver.ResolveWholeString(this.ConnectionStringSetting);
                    this.LeasesTableName = resolver.ResolveWholeString(this.LeasesTableName);
                }
            }
        }
    }
}