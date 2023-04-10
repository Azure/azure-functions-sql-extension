// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using static Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry.Telemetry;
using Xunit;
using Microsoft.Data.SqlClient;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    /// <summary>
    /// Test fixture containing one-time setup code for Input Binding Integration tests. See https://xunit.net/docs/shared-context for more details
    /// </summary>
    public class SqlInputOutputBindingIntegrationTestFixture : IDisposable
    {
        /// <summary>
        /// List of all functions in the samples folder that will be started before the
        /// input and output binding tests are run. Some functions are not included because
        /// they may interfere with other tests (ex. TimerTriggerProducts).
        /// </summary>
        private readonly List<string> SampleFunctions = new() { "GetProducts", "GetProductsStoredProcedure", "GetProductsNameEmpty", "GetProductsStoredProcedureFromAppSetting", "GetProductNamesView", "AddProduct", "AddProductParams", "AddProductsArray", "AddProductWithIdentityColumn", "AddProductsWithIdentityColumnArray", "AddProductWithIdentityColumnIncluded", "AddProductWithMultiplePrimaryColumnsAndIdentity", "GetAndAddProducts", "AddProductWithDefaultPK" };

        /// <summary>
        /// List of all functions in the test folder that will be started before the
        /// input and output binding tests are run.
        /// </summary>
        private readonly List<string> TestFunctions = new() { "GetProductsColumnTypesSerialization", "AddProductColumnTypes", "AddProductExtraColumns", "AddProductMissingColumns", "AddProductMissingColumnsExceptionFunction", "AddProductsNoPartialUpsert", "AddProductIncorrectCasing", "AddProductDefaultPKAndDifferentColumnOrder" };

        /// <summary>
        /// Host process for Azurite local storage emulator. This is required for non-HTTP trigger functions:
        /// https://docs.microsoft.com/azure/azure-functions/functions-develop-local
        /// </summary>
        private Process AzuriteHost;

        /// <summary>
        /// Connection string to the master database on the test server, mainly used for database setup and teardown.
        /// </summary>
        private string MasterConnectionString;

        /// <summary>
        /// Name of the database used.
        /// </summary>
        private string DatabaseName;

        /// <summary>
        /// The port the Functions Host is running on. Default is 7071.
        /// </summary>
        private readonly int Port = 7071;

        /// <summary>
        /// Host processes for Azure Function CLI.
        /// </summary>
        private List<Process> FunctionHostList { get; } = new List<Process>();

        public SqlInputOutputBindingIntegrationTestFixture()
        {
            this.StartAzurite();
            this.SetupDatabase();
            this.StartFunctionHosts();
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
        /// Sets up a test database for the tests to use.
        /// </summary>
        /// <remarks>
        /// The server the database will be created on can be set by the environment variable "TEST_SERVER", otherwise localhost will be used by default.
        /// By default, integrated authentication will be used to connect to the server, unless the env variable "SA_PASSWORD" is set.
        /// In this case, connection will be made using SQL login with user "SA" and the provided password.
        /// </remarks>
        private void SetupDatabase()
        {
            SqlConnectionStringBuilder connectionStringBuilder;
            string connectionString = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING");
            if (connectionString != null)
            {
                this.MasterConnectionString = connectionString;
                connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            }
            else
            {
                // Get the test server name from environment variable "TEST_SERVER", default to localhost if not set
                string testServer = Environment.GetEnvironmentVariable("TEST_SERVER");
                if (string.IsNullOrEmpty(testServer))
                {
                    testServer = "localhost";
                }

                // First connect to master to create the database
                connectionStringBuilder = new SqlConnectionStringBuilder()
                {
                    DataSource = testServer,
                    InitialCatalog = "master",
                    Pooling = false,
                    Encrypt = SqlConnectionEncryptOption.Optional
                };

                // Either use integrated auth or SQL login depending if SA_PASSWORD is set
                string userId = "SA";
                string password = Environment.GetEnvironmentVariable("SA_PASSWORD");
                if (string.IsNullOrEmpty(password))
                {
                    connectionStringBuilder.IntegratedSecurity = true;
                }
                else
                {
                    connectionStringBuilder.UserID = userId;
                    connectionStringBuilder.Password = password;
                }
                this.MasterConnectionString = connectionStringBuilder.ToString();
            }

            // Create database
            // Retry this in case the server isn't fully initialized yet
            this.DatabaseName = TestUtils.GetUniqueDBName("SqlBindingsTest");
            TestUtils.Retry(() =>
            {
                using var masterConnection = new SqlConnection(this.MasterConnectionString);
                masterConnection.Open();
                TestUtils.ExecuteNonQuery(masterConnection, $"CREATE DATABASE [{this.DatabaseName}]");
            });

            connectionStringBuilder.InitialCatalog = this.DatabaseName;

            // Set SqlConnectionString env var for the tests to use
            Environment.SetEnvironmentVariable("SqlConnectionString", connectionStringBuilder.ToString());
        }

        /// <summary>
        /// This starts the input binding function hosts.
        /// </summary>
        private void StartFunctionHosts()
        {
            string binPath = TestUtils.GetPathToBin();
            foreach (SupportedLanguages lang in Enum.GetValues(typeof(SupportedLanguages)))
            {
                if (lang == SupportedLanguages.CSharp)
                {
                    this.StartFunctionHost(Path.Combine(binPath, "SqlExtensionSamples", "CSharp"), this.SampleFunctions);
                    this.StartFunctionHost(Path.Combine(binPath), this.TestFunctions);
                }
                else if (lang == SupportedLanguages.Java)
                {
                    this.StartFunctionHost(Path.Combine(binPath, "SqlExtensionSamples", "Java", "target", "azure-functions", "samples-java-1665766173929"), this.SampleFunctions);
                    this.StartFunctionHost(Path.Combine(binPath, "..", "..", "..", "Integration", "test-java", "target", "azure-functions", "test-java-1666041146813"), this.TestFunctions);
                }
                else if (lang == SupportedLanguages.OutOfProc)
                {
                    this.StartFunctionHost(Path.Combine(binPath, "SqlExtensionSamples", "OutOfProc"), this.SampleFunctions);
                    this.StartFunctionHost(Path.Combine(binPath, "SqlExtensionSamples", "OutOfProc", "test"), this.TestFunctions);
                }
                else
                {
                    this.StartFunctionHost(Path.Combine(binPath, "SqlExtensionSamples", Enum.GetName(typeof(SupportedLanguages), lang)));
                }
            }
        }

        private void StartFunctionHost(string workingDirectory, List<string> functions = null)
        {
            string functionsArg = " --functions ";
            functionsArg += functions != null ? string.Join(" ", functions) : string.Join(" ", this.SampleFunctions) + " " + string.Join(" ", this.TestFunctions);
            // Use a different port for each new host process, starting with the default port number: 7071.
            int port = this.Port + this.FunctionHostList.Count;

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

            try
            {
                // Drop the test database
                using var masterConnection = new SqlConnection(this.MasterConnectionString);
                masterConnection.Open();
                TestUtils.ExecuteNonQuery(masterConnection, $"DROP DATABASE IF EXISTS {this.DatabaseName}");
            }
            catch (Exception e4)
            {
                Console.WriteLine($"Failed to drop {this.DatabaseName}, Error: {e4.Message}");
            }

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