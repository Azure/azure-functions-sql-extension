// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    /// <summary>
    /// Test fixture containing one-time setup code for Integration tests. See https://xunit.net/docs/shared-context for more details
    /// </summary>
    public class IntegrationTestFixture : BaseTestFixture
    {
        public IntegrationTestFixture() : base(true) { }
    }

    /// <summary>
    /// Base test fixture - xUnit doesn't allow parameterized constructors so the benchmark tests will use this directly.
    /// </summary>
    public class BaseTestFixture : IDisposable
    {
        /// <summary>
        /// Host process for Azurite local storage emulator. This is required for non-HTTP trigger functions:
        /// https://docs.microsoft.com/azure/azure-functions/functions-develop-local
        /// </summary>
        private Process AzuriteHost;

        public BaseTestFixture()
        {
            this.StartAzurite();
        }

        /// <summary>
        /// This starts the Azurite storage emulator.
        /// </summary>
        protected void StartAzurite()
        {
            Console.WriteLine("Starting Azurite Host...");
            this.AzuriteHost = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "azurite",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                }
            };

            this.AzuriteHost.Start();
        }

        public void Dispose()
        {
            try
            {
                this.AzuriteHost.Kill(true);
                this.AzuriteHost.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to stop Azurite, Error: {e.Message}");
            }
            GC.SuppressFinalize(this);
        }
    }
}
