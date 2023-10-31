// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    public static class TestHelpers
    {
        /// <summary>
        /// Helper that builds a test host to configure the options type specified.
        /// </summary>
        /// <typeparam name="TOptions">The options type to configure.</typeparam>
        /// <param name="configure">Delegate used to configure the target extension.</param>
        /// <param name="configValues">Set of test configuration values to apply.</param>
        /// <param name="env"></param>
        /// <returns></returns>
        public static TOptions GetConfiguredOptions<TOptions>(Action<IWebJobsBuilder> configure, Dictionary<string, string> configValues, string env = default) where TOptions : class, new()
        {
            IHostBuilder hostBuilder = new HostBuilder()
                .ConfigureDefaultTestHost<TestProgram>(b =>
                {
                    configure(b);
                })
                .ConfigureAppConfiguration(cb =>
                {
                    cb.AddInMemoryCollection(configValues);
                });

            if (env != default)
            {
                hostBuilder.UseEnvironment(env);
            }

            IHost host = hostBuilder.Build();

            TOptions options = host.Services.GetRequiredService<IOptions<TOptions>>().Value;
            return options;
        }

        public static IHostBuilder ConfigureDefaultTestHost(this IHostBuilder builder, Action<IWebJobsBuilder> configureWebJobs, params Type[] types)
        {
            return builder
                .ConfigureWebJobs(configureWebJobs)
                .ConfigureAppConfiguration(c =>
                {
                    c.AddTestSettings();
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ITypeLocator>(new FakeTypeLocator(types));

                    // Register this to fail a test if a background exception is thrown
                    services.AddSingleton<IWebJobsExceptionHandlerFactory, TestExceptionHandlerFactory>();
                })
                .ConfigureTestLogger();
        }

        public static IHostBuilder ConfigureDefaultTestHost<TProgram>(this IHostBuilder builder,
           Action<IWebJobsBuilder> configureWebJobs)
        {
            return builder.ConfigureDefaultTestHost(configureWebJobs, typeof(TProgram));
        }

        public static IHostBuilder ConfigureDefaultTestHost<TProgram>(this IHostBuilder builder, Action<IWebJobsBuilder> configureWebJobs,
            INameResolver nameResolver = null, IJobActivator activator = null)
        {
            return builder.ConfigureDefaultTestHost(configureWebJobs, typeof(TProgram))
                .ConfigureServices(services =>
                {
#pragma warning disable IDE0001
                    services.AddSingleton<IJobHost, JobHost>();

                    if (nameResolver != null)
                    {
                        services.AddSingleton<INameResolver>(nameResolver);
                    }

                    if (activator != null)
                    {
                        services.AddSingleton<IJobActivator>(activator);
                    }
                });
        }
#pragma warning restore IDE0001
        public static IHostBuilder ConfigureTestLogger(this IHostBuilder builder)
        {
            return builder.ConfigureLogging(logging =>
             {
                 logging.AddProvider(new TestLoggerProvider());
             });
        }

    }

    public class TestProgram
    {
    }
}
