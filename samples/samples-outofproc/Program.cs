// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // #if DEBUG
            //     Debugger.Launch();
            // #endif
            //<docsnippet_startup>
            var host = new HostBuilder()
                //<docsnippet_configure_defaults>
                .ConfigureFunctionsWorkerDefaults(builder =>
                {
                    builder
                        .AddApplicationInsights()
                        .AddApplicationInsightsLogger();
                })
                //</docsnippet_configure_defaults>
                //<docsnippet_dependency_injection>
                .ConfigureServices(s =>
                {
                    s.AddSingleton<IHttpResponderService, DefaultHttpResponderService>();
                })
                //</docsnippet_dependency_injection>
                .Build();
            //</docsnippet_startup>

            //<docsnippet_host_run>
            await host.RunAsync();
            //</docsnippet_host_run>
        }
    }
}