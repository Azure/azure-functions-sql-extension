// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Azure.WebJobs.Host.Scale;

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

            builder.Services.TryAddSingleton<SqlTriggerBindingProvider>();

            builder.AddExtension<SqlExtensionConfigProvider>().BindOptions<SqlOptions>();
            if (configureSqlOptions != null)
            {
#pragma warning disable IDE0001 // Cannot simplify the name here, supressing the warning.
                builder.Services.Configure<SqlOptions>(configureSqlOptions);
#pragma warning restore IDE0001
            }

            return builder;
        }

        public static IWebJobsBuilder AddSqlScaleForTrigger(this IWebJobsBuilder builder, TriggerMetadata triggerMetadata)
        {
            IServiceProvider serviceProvider = null;
            var scalerProvider = new Lazy<SqlScalerProvider>(() => new SqlScalerProvider(serviceProvider, triggerMetadata));
            builder.Services.AddSingleton((Func<IServiceProvider, IScaleMonitorProvider>)delegate (IServiceProvider resolvedServiceProvider)
            {
                serviceProvider = serviceProvider ?? resolvedServiceProvider;
                return scalerProvider.Value;
            });
            builder.Services.AddSingleton((Func<IServiceProvider, ITargetScalerProvider>)delegate (IServiceProvider resolvedServiceProvider)
            {
                serviceProvider = serviceProvider ?? resolvedServiceProvider;
                return scalerProvider.Value;
            });
            return builder;
        }
    }
}