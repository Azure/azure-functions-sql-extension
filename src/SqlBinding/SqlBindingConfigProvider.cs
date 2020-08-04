// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Data.SqlClient;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Configuration;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlConverters;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Exposes SQL input, output, and trigger bindings
    /// </summary>
    [Extension("sql")]
    internal class SqlBindingConfigProvider : IExtensionConfigProvider
    {
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlBindingConfigProvider/>"/> class.
        /// </summary>
        /// <param name="configuration"></param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the configuration is null
        /// </exception>
        public SqlBindingConfigProvider(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Initializes the SQL binding rules
        /// </summary>
        /// <param name="context"> The config context </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if context is null
        /// </exception>
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            var inputOutputRule = context.AddBindingRule<SqlAttribute>();
            var converter = new SqlConverter(_configuration);
            inputOutputRule.BindToInput<SqlCommand>(converter);
            inputOutputRule.BindToInput<string>(typeof(SqlGenericsConverter<string>), _configuration);
            inputOutputRule.BindToCollector<OpenType>(typeof(SqlAsyncCollectorBuilder<>), _configuration);
            inputOutputRule.BindToInput<OpenType>(typeof(SqlGenericsConverter<>), _configuration);

            var triggerRule = context.AddBindingRule<SqlTriggerAttribute>();
            triggerRule.BindToTrigger<OpenType>(new SqlTriggerAttributeBindingProvider(_configuration));
            triggerRule.BindToValueProvider<OpenType>(SqlTriggerBinding.GetBinder);
            // Change this to worker table row. And the Converter probably needs to implement some sort of interface, have to
            // find a binding that uses this function
            //triggerRule.AddOpenConverter<List<Dictionary<string, string>>, OpenType>(
            //    typeof(SqlChangeTrackingConverter<>), _configuration);
        }
    }
}