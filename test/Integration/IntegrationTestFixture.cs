// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Xunit;
using static Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry.Telemetry;
using System.Linq;

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
        private readonly Process AzuriteHost;

        /// <summary>
        /// Connection string to the master database on the test server, mainly used for database setup and teardown.
        /// </summary>
        private readonly string MasterConnectionString;

        /// <summary>
        /// Name of the database used.
        /// </summary>
        private readonly string DatabaseName;

        /// <summary>
        /// List of all functions in the samples folder that will be started before the
        /// input and output binding tests are run. Some functions are not included because
        /// (1) they may interfere with other tests (ex. TimerTriggerProducts) or
        /// (2) they don't apply to all languages (ex. AddProductsCollector)
        /// </summary>
        private readonly List<string> SampleFunctions = new() { "GetProducts", "GetProductsStoredProcedure", "GetProductsNameEmpty", "GetProductsStoredProcedureFromAppSetting", "GetProductNamesView", "AddProduct", "AddProductParams", "AddProductsArray", "AddProductWithMultiplePrimaryColumnsAndIdentity", "GetAndAddProducts", "AddProductWithDefaultPK" };

        /// <summary>
        /// List of all functions in the test folder that will be started before the
        /// input and output binding tests are run.
        /// </summary>
        private readonly List<string> TestFunctions = new() { "GetProductsColumnTypesSerialization", "AddProductColumnTypes", "AddProductExtraColumns", "AddProductMissingColumns", "AddProductMissingColumnsExceptionFunction", "AddProductsNoPartialUpsert", "AddProductIncorrectCasing", "AddProductDefaultPKAndDifferentColumnOrder" };

        /// <summary>
        /// Host processes for Azure Function CLI.
        /// </summary>
        public List<Process> FunctionHostList { get; } = new List<Process>();

        public IntegrationTestFixture()
        {
            this.AzuriteHost = TestUtils.StartAzurite();
            TestUtils.SetupDatabase(out this.MasterConnectionString, out this.DatabaseName);
            this.StartFunctionHosts();
        }

        /// <summary>
        /// This starts the function hosts for each language.
        /// </summary>
        private void StartFunctionHosts()
        {
            string binPath = TestUtils.GetPathToBin();
            // Only start CSharp host for CSharp only tests task to ensure code coverage shows in pipeline.
            string languages = Environment.GetEnvironmentVariable("LANGUAGES_TO_TEST");
            SupportedLanguages[] supportedLanguages = languages == null ? (SupportedLanguages[])Enum.GetValues(typeof(SupportedLanguages))
                : languages.Split(',').Select(l => (SupportedLanguages)Enum.Parse(typeof(SupportedLanguages), l)).ToArray();
            foreach (SupportedLanguages lang in supportedLanguages)
            {
                if (lang == SupportedLanguages.CSharp)
                {
                    this.StartHost(Path.Combine(binPath, "SqlExtensionSamples", "CSharp"), TestUtils.GetPort(lang), this.SampleFunctions);
                    this.StartHost(Path.Combine(binPath), TestUtils.GetPort(lang, true), this.TestFunctions);
                }
                else if (lang == SupportedLanguages.Java)
                {
                    this.StartHost(Path.Combine(binPath, "SqlExtensionSamples", "Java", "target", "azure-functions", "samples-java-1665766173929"), TestUtils.GetPort(lang), this.SampleFunctions);
                    this.StartHost(Path.Combine(binPath, "..", "..", "..", "Integration", "test-java", "target", "azure-functions", "test-java-1666041146813"), TestUtils.GetPort(lang, true), this.TestFunctions);
                }
                else if (lang == SupportedLanguages.OutOfProc)
                {
                    this.StartHost(Path.Combine(binPath, "SqlExtensionSamples", "OutOfProc"), TestUtils.GetPort(lang), this.SampleFunctions);
                    this.StartHost(Path.Combine(binPath, "SqlExtensionSamples", "OutOfProc", "test"), TestUtils.GetPort(lang, true), this.TestFunctions);
                }
                else
                {
                    this.StartHost(Path.Combine(binPath, "SqlExtensionSamples", Enum.GetName(typeof(SupportedLanguages), lang)), TestUtils.GetPort(lang), null);
                }
            }
        }

        private void StartHost(string workingDirectory, int port, List<string> functions = null)
        {
            string functionsArg = " --functions ";
            functionsArg += functions != null ? string.Join(" ", functions) : string.Join(" ", this.SampleFunctions) + " " + string.Join(" ", this.TestFunctions);

            var startInfo = new ProcessStartInfo
            {
                // The full path to the Functions CLI is required in the ProcessStartInfo because UseShellExecute is set to false.
                // We cannot both use shell execute and redirect output at the same time: https://docs.microsoft.com//dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput#remarks
                FileName = TestUtils.GetFunctionsCoreToolsPath(),
                Arguments = $"start --verbose --port {port} {functionsArg}",
                WorkingDirectory = workingDirectory,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            // Always disable telemetry during test runs
            startInfo.EnvironmentVariables[TelemetryOptoutEnvVar] = "1";

            Console.WriteLine($"Starting {startInfo.FileName} {startInfo.Arguments} in {startInfo.WorkingDirectory}");

            var functionHost = new Process
            {
                StartInfo = startInfo
            };

            this.FunctionHostList.Add(functionHost);

            // Register all handlers before starting the functions host process.
            var taskCompletionSource = new TaskCompletionSource<bool>();
            void SignalStartupHandler(object sender, DataReceivedEventArgs e)
            {
                // This string is printed after the function host is started up - use this to ensure that we wait long enough
                // since sometimes the host can take a little while to fully start up
                if (e.Data?.Contains(" Host initialized ") == true)
                {
                    taskCompletionSource.SetResult(true);
                }
            };
            functionHost.OutputDataReceived += SignalStartupHandler;

            functionHost.Start();
            functionHost.OutputDataReceived += this.GetTestOutputHandler(functionHost.Id);
            functionHost.ErrorDataReceived += this.GetTestOutputHandler(functionHost.Id);
            functionHost.BeginOutputReadLine();
            functionHost.BeginErrorReadLine();

            Console.WriteLine($"Waiting for Azure Function host in {workingDirectory} to start...");

            const int FunctionHostStartupTimeoutInSeconds = 60;
            bool isCompleted = taskCompletionSource.Task.Wait(TimeSpan.FromSeconds(FunctionHostStartupTimeoutInSeconds));
            Assert.True(isCompleted, $"Functions host in {workingDirectory} did not start within specified time.");

            // Give additional time to Functions host to setup routes for the HTTP triggers so that the HTTP requests
            // made from the test methods do not get refused.
            const int BufferTimeInSeconds = 5;
            Task.Delay(TimeSpan.FromSeconds(BufferTimeInSeconds)).Wait();

            Console.WriteLine($"Azure Function host in {workingDirectory} started!");
            functionHost.OutputDataReceived -= SignalStartupHandler;
        }

        private DataReceivedEventHandler GetTestOutputHandler(int processId)
        {
            void TestOutputHandler(object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine($"[{processId}] {e.Data}");
                }
            }
            return TestOutputHandler;
        }

        public void Dispose()
        {
            this.DisposeFunctionHosts();
            TestUtils.StopAzurite(this.AzuriteHost);
            TestUtils.DropDatabase(this.MasterConnectionString, this.DatabaseName);

            GC.SuppressFinalize(this);
        }

        public void DisposeFunctionHosts()
        {
            foreach (Process functionHost in this.FunctionHostList)
            {
                try
                {
                    functionHost.CancelOutputRead();
                    functionHost.CancelErrorRead();
                    functionHost.Kill(true);
                    functionHost.WaitForExit();
                    functionHost.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to stop function host, Error: {ex.Message}");
                }
            }
            this.FunctionHostList.Clear();
        }
    }
}
