// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Azure.WebJobs.Host.Scale;
using System.Threading.Tasks;

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
#pragma warning disable IDE0001 // Cannot simplify the name here, suppressing the warning.
                builder.Services.Configure<SqlOptions>(configureSqlOptions);
#pragma warning restore IDE0001
            }

            return builder;
        }

        internal static IWebJobsBuilder AddSqlScaleForTrigger(this IWebJobsBuilder builder, TriggerMetadata triggerMetadata)
        {
            IServiceProvider serviceProvider = null;
            var scalerProviderTask = new Lazy<Task<SqlScalerProvider>>(() =>
                SqlScalerProvider.CreateAsync(serviceProvider, triggerMetadata));

            builder.Services.AddSingleton((Func<IServiceProvider, IScaleMonitorProvider>)(resolvedServiceProvider =>
            {
                serviceProvider = serviceProvider ?? resolvedServiceProvider;
                // Wait for the async initialization to complete
                return scalerProviderTask.Value.GetAwaiter().GetResult();
            }));

            builder.Services.AddSingleton((Func<IServiceProvider, ITargetScalerProvider>)(resolvedServiceProvider =>
            {
                serviceProvider = serviceProvider ?? resolvedServiceProvider;
                // Wait for the async initialization to complete
                return scalerProviderTask.Value.GetAwaiter().GetResult();
            }));

            return builder;
        }
    }
}