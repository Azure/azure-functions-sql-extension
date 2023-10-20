// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    public static class SqlBindingExtension
    {
        public static IWebJobsBuilder AddSql(this IWebJobsBuilder builder, Action<SqlOptions> configureSqlOptions = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<SqlBindingConfigProvider>().BindOptions<SqlOptions>();
            if (configureSqlOptions != null)
            {
#pragma warning disable IDE0001
                _ = builder.Services.Configure<SqlOptions>(configureSqlOptions);
#pragma warning restore IDE0001
            }
            builder.Services.AddOptions<SqlOptions>()
                .Configure<IHostingEnvironment>((options, env) =>
                {
                    if (env.IsDevelopment() && options.PollingIntervalMs == SqlOptions.DefaultPollingIntervalMs)
                    {
                        options.PollingIntervalMs = 2000;
                    }
                });

            return builder;
        }
    }
}