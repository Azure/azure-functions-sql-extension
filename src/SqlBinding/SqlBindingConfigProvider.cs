// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Data.SqlClient;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Configuration;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlConverters;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Exposes Sql input, output, and trigger bindings
    /// </summary>
    [Extension("sql")]
    public class SqlBindingConfigProvider : IExtensionConfigProvider
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
            var rule = context.AddBindingRule<SqlAttribute>();
            var converter = new SqlConverter(_configuration);
            rule.BindToInput<SqlCommand>(converter);
            rule.BindToInput<string>(typeof(SqlGenericsConverter<string>), _configuration);
            rule.BindToCollector<OpenType>(typeof(SqlAsyncCollectorBuilder<>), _configuration);
            rule.BindToInput<OpenType>(typeof(SqlGenericsConverter<>), _configuration);
        }
    }
}