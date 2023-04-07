// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;

namespace DotnetIsolatedTests
{
    internal class Program
    {
        public static void Main()
        {
            IHost host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .Build();

            host.Run();
        }
    }
}

