// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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

        public IntegrationTestFixture()
        {
            this.StartAzurite();
            InstallPythonPackages();
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
        /// This installs the azure-functions-1.11.3b1 package which is temporarily needed for SQL Bindings to work.
        /// More information: https://github.com/Azure/azure-functions-sql-extension/issues/250
        /// </summary>
        protected static void InstallPythonPackages()
        {
            Console.WriteLine("Installing azure-functions-1.11.3b1...");

            string workingDirectory = Path.Combine(TestUtils.GetPathToBin(), "..", "..", "..", "..", "samples", "samples-python");
            var python = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = $"-m pip install -r requirements.txt",
                    WorkingDirectory = workingDirectory,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            var taskCompletionSource = new TaskCompletionSource<bool>();
            void SignalStartupHandler(object sender, DataReceivedEventArgs e)
            {
                // This string is printed after the packages are installed
                if (e.Data?.Contains("Successfully installed azure-functions-1.11.3b1") == true ||
                    e.Data?.Contains("Requirement already satisfied: azure-functions==1.11.3b1") == true)
                {
                    taskCompletionSource.SetResult(true);
                }
            };
            python.OutputDataReceived += SignalStartupHandler;
            python.Start();
            python.OutputDataReceived += GetTestOutputHandler();
            python.ErrorDataReceived += GetTestOutputHandler();
            python.BeginOutputReadLine();
            python.BeginErrorReadLine();

            const int PipPackagesInstallTimeoutInSeconds = 60;
            bool isCompleted = taskCompletionSource.Task.Wait(TimeSpan.FromSeconds(PipPackagesInstallTimeoutInSeconds));
            Assert.True(isCompleted, "azure-functions-1.11.3b1 was not installed in specified time.");
            python.OutputDataReceived -= SignalStartupHandler;
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

        private static DataReceivedEventHandler GetTestOutputHandler()
        {
            static void TestOutputHandler(object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine($"{e.Data}");
                }
            }
            return TestOutputHandler;
        }
    }
}
