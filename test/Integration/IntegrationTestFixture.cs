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
    public class IntegrationTestFixture : IDisposable
    {
        /// <summary>
        /// Host process for Azurite local storage emulator. This is required for non-HTTP trigger functions:
        /// https://docs.microsoft.com/azure/azure-functions/functions-develop-local
        /// </summary>
        private Process AzuriteHost;

        public IntegrationTestFixture(bool buildJava = true)
        {
            this.StartAzurite();
            if (buildJava)
            {
                BuildJavaFunctionApps();
            }

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

        /// <summary>
        /// Build the samples-java and test-java projects.
        /// </summary>
        private static void BuildJavaFunctionApps()
        {
            string samplesJavaPath = Path.Combine(TestUtils.GetPathToBin(), "SqlExtensionSamples", "Java");
            BuildJavaFunctionApp(samplesJavaPath);

            string testJavaPath = Path.Combine(TestUtils.GetPathToBin(), "..", "..", "..", "Integration", "test-java");
            BuildJavaFunctionApp(testJavaPath);
        }

        /// <summary>
        /// Run `mvn clean package` to build the Java function app.
        /// </summary>
        private static void BuildJavaFunctionApp(string workingDirectory)
        {
            var maven = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "mvn",
                    Arguments = "clean package",
                    WorkingDirectory = workingDirectory,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                }
            };

            maven.Start();

            const int buildJavaAppTimeoutInMs = 60000;
            maven.WaitForExit(buildJavaAppTimeoutInMs);

            bool isCompleted = maven.ExitCode == 0;
            Assert.True(isCompleted, "Java function app did not build successfully");
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
