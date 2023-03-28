// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Microsoft.Data.SqlClient;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry.Telemetry;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public class IntegrationTestBase : IDisposable
    {
        /// <summary>
        /// The first Function Host process that was started. Null if no process has been started yet.
        /// </summary>
        protected Process FunctionHost => this.FunctionHostList.FirstOrDefault();

        /// <summary>
        /// Host processes for Azure Function CLI.
        /// </summary>
        protected List<Process> FunctionHostList { get; } = new List<Process>();

        /// <summary>
        /// Connection to the database for the current test.
        /// </summary>
        private DbConnection Connection;

        /// <summary>
        /// Connection string to the database created for the test
        /// </summary>
        protected string DbConnectionString { get; private set; }

        /// <summary>
        /// Name of the database used for the current test.
        /// </summary>
        protected string DatabaseName { get; private set; }

        /// <summary>
        /// Output redirect for XUnit tests.
        /// Please use LogOutput() instead of Console or Debug.
        /// </summary>
        protected ITestOutputHelper TestOutput { get; private set; }

        /// <summary>
        /// The port the Functions Host is running on. Default is 7071.
        /// </summary>
        protected int Port { get; private set; } = 7071;

        public IntegrationTestBase(ITestOutputHelper output = null)
        {
            this.TestOutput = output;
            this.SetupDatabaseObjects();
        }

        /// <summary>
        /// Sets up the tables, views, and stored procedures needed for tests in the database created
        /// in IntegrationTestFixture.
        /// </summary>
        private void SetupDatabaseObjects()
        {
            // Get the connection string from the environment variable set in IntegrationTestFixture
            this.DbConnectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
            this.Connection = new SqlConnection(this.DbConnectionString);
            this.Connection.Open();
            this.DatabaseName = this.Connection.Database;
            // Create these in a specific order since things like views require that their underlying objects have been created already
            // Ideally all the sql files would be in a sqlproj and can just be deployed
            this.ExecuteAllScriptsInFolder(Path.Combine(TestUtils.GetPathToBin(), "Database", "Tables"));
            // Separate DROP and CREATE for views and procedures  since CREATE VIEW/PROCEDURE needs to be the first statement in the batch
            this.ExecuteAllScriptsInFolder(Path.Combine(TestUtils.GetPathToBin(), "Database", "Views", "Drop"));
            this.ExecuteAllScriptsInFolder(Path.Combine(TestUtils.GetPathToBin(), "Database", "Views"));
            this.ExecuteAllScriptsInFolder(Path.Combine(TestUtils.GetPathToBin(), "Database", "StoredProcedures", "Drop"));
            this.ExecuteAllScriptsInFolder(Path.Combine(TestUtils.GetPathToBin(), "Database", "StoredProcedures"));
        }

        private void ExecuteAllScriptsInFolder(string folder)
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*.sql"))
            {
                this.LogOutput($"Executing script ${file}");
                this.ExecuteNonQuery(File.ReadAllText(file));
            }
        }

        /// <summary>
        /// This starts the Functions runtime with the specified function(s).
        /// </summary>
        /// <remarks>
        /// - The functionName is different than its route.<br/>
        /// - You can start multiple functions by passing in a space-separated list of function names.<br/>
        /// </remarks>
        public void StartFunctionHost(string functionName, SupportedLanguages language, bool useTestFolder = false, DataReceivedEventHandler customOutputHandler = null, IDictionary<string, string> environmentVariables = null)
        {
            string workingDirectory = language == SupportedLanguages.CSharp && useTestFolder ? TestUtils.GetPathToBin() : Path.Combine(TestUtils.GetPathToBin(), "SqlExtensionSamples", Enum.GetName(typeof(SupportedLanguages), language));
            if (language == SupportedLanguages.Java)
            {
                workingDirectory = useTestFolder ? Path.Combine(TestUtils.GetPathToBin(), "..", "..", "..", "Integration", "test-java") : workingDirectory;
                string projectName = useTestFolder ? "test-java-1666041146813" : "samples-java-1665766173929";
                workingDirectory = Path.Combine(workingDirectory, "target", "azure-functions", projectName);
            }
            if (language == SupportedLanguages.OutOfProc && useTestFolder)
            {
                workingDirectory = Path.Combine(workingDirectory, "test");
            }

            if (!Directory.Exists(workingDirectory))
            {
                throw new FileNotFoundException("Working directory not found at " + workingDirectory);
            }

            // Use a different port for each new host process, starting with the default port number: 7071.
            int port = this.Port + this.FunctionHostList.Count;

            var startInfo = new ProcessStartInfo
            {
                // The full path to the Functions CLI is required in the ProcessStartInfo because UseShellExecute is set to false.
                // We cannot both use shell execute and redirect output at the same time: https://docs.microsoft.com//dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput#remarks
                FileName = GetFunctionsCoreToolsPath(),
                Arguments = $"start --verbose --port {port} --functions {functionName}",
                WorkingDirectory = workingDirectory,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            environmentVariables?.ToList().ForEach(ev => startInfo.EnvironmentVariables[ev.Key] = ev.Value);

            // Always disable telemetry during test runs
            startInfo.EnvironmentVariables[TelemetryOptoutEnvVar] = "1";

            this.LogOutput($"Starting {startInfo.FileName} {startInfo.Arguments} in {startInfo.WorkingDirectory}");

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
            functionHost.OutputDataReceived += customOutputHandler;

            functionHost.Start();
            functionHost.OutputDataReceived += this.GetTestOutputHandler(functionHost.Id);
            functionHost.ErrorDataReceived += this.GetTestOutputHandler(functionHost.Id);
            functionHost.BeginOutputReadLine();
            functionHost.BeginErrorReadLine();

            this.LogOutput("Waiting for Azure Function host to start...");

            const int FunctionHostStartupTimeoutInSeconds = 60;
            bool isCompleted = taskCompletionSource.Task.Wait(TimeSpan.FromSeconds(FunctionHostStartupTimeoutInSeconds));
            Assert.True(isCompleted, "Functions host did not start within specified time.");

            // Give additional time to Functions host to setup routes for the HTTP triggers so that the HTTP requests
            // made from the test methods do not get refused.
            const int BufferTimeInSeconds = 5;
            Task.Delay(TimeSpan.FromSeconds(BufferTimeInSeconds)).Wait();

            this.LogOutput("Azure Function host started!");
            functionHost.OutputDataReceived -= SignalStartupHandler;
        }

        private static string GetFunctionsCoreToolsPath()
        {
            // Determine npm install path from either env var set by pipeline or OS defaults
            // Pipeline env var is needed as the Windows hosted agents installs to a non-traditional location
            string nodeModulesPath = Environment.GetEnvironmentVariable("NODE_MODULES_PATH");
            if (string.IsNullOrEmpty(nodeModulesPath))
            {
                nodeModulesPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"npm\node_modules\") :
                    @"/usr/local/lib/node_modules";
            }

            string funcExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "func.exe" : "func";
            string funcPath = Path.Combine(nodeModulesPath, "azure-functions-core-tools", "bin", funcExe);

            if (!File.Exists(funcPath))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Search Program Files folder as well
                    string programFilesFuncPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Azure Functions Core Tools", funcExe);
                    if (File.Exists(programFilesFuncPath))
                    {
                        return programFilesFuncPath;
                    }
                    throw new FileNotFoundException($"Azure Function Core Tools not found at {funcPath} or {programFilesFuncPath}");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // Search Mac to see if brew installed location has azure function core tools
                    string usrBinFuncPath = Path.Combine("/usr", "local", "bin", "func");
                    if (File.Exists(usrBinFuncPath))
                    {
                        return usrBinFuncPath;
                    }
                    throw new FileNotFoundException($"Azure Function Core Tools not found at {funcPath} or {usrBinFuncPath}");
                }
                throw new FileNotFoundException($"Azure Function Core Tools not found at {funcPath}");
            }

            return funcPath;
        }

        protected void LogOutput(string output)
        {
            if (this.TestOutput != null)
            {
                this.TestOutput.WriteLine(output);
            }
            else
            {
                Console.WriteLine(output);
            }
        }

        private DataReceivedEventHandler GetTestOutputHandler(int processId)
        {
            void TestOutputHandler(object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    this.LogOutput($"[{processId}] {e.Data}");
                }
            }
            return TestOutputHandler;
        }

        protected async Task<HttpResponseMessage> SendGetRequest(string requestUri, bool verifySuccess = true)
        {
            string timeStamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
            this.LogOutput($"[{timeStamp}] Sending GET request: {requestUri}");

            if (string.IsNullOrEmpty(requestUri))
            {
                throw new ArgumentException("URI cannot be null or empty.");
            }

            var client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(requestUri);

            if (verifySuccess)
            {
                Assert.True(response.IsSuccessStatusCode, $"Http request failed with code {response.StatusCode}. Please check output for more detailed message.");
            }

            return response;
        }

        protected async Task<HttpResponseMessage> SendPostRequest(string requestUri, string json, bool verifySuccess = true)
        {
            this.LogOutput("Sending POST request: " + requestUri);

            if (string.IsNullOrEmpty(requestUri))
            {
                throw new ArgumentException("URI cannot be null or empty.");
            }

            var client = new HttpClient();
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(requestUri, content);

            if (verifySuccess)
            {
                Assert.True(response.IsSuccessStatusCode, $"Http request failed with code {response.StatusCode}. Please check output for more detailed message.");
            }

            return response;
        }

        /// <summary>
        /// Executes a command against the current connection.
        /// </summary>
        /// <param name="commandText">Command text to execute</param>
        /// <param name="message">Optional message to write when this query is executed. Defaults to writing the query commandText</param>
        protected void ExecuteNonQuery(string commandText, string message = null)
        {
            TestUtils.ExecuteNonQuery(this.Connection, commandText, this.LogOutput, message: message);
        }

        /// <summary>
        /// Executes a command against the current connection and the result is returned.
        /// </summary>
        protected object ExecuteScalar(string commandText)
        {
            return TestUtils.ExecuteScalar(this.Connection, commandText, this.LogOutput);
        }


        public void Dispose()
        {
            // Try to clean up after test run, but don't consider it a failure if we can't for some reason
            try
            {
                this.Connection.Close();
                this.Connection.Dispose();
            }
            catch (Exception e1)
            {
                this.LogOutput($"Failed to close connection. Error: {e1.Message}");
            }

            this.DisposeFunctionHosts();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes all the running function hosts
        /// </summary>
        protected void DisposeFunctionHosts()
        {
            foreach (Process functionHost in this.FunctionHostList)
            {
                try
                {
                    functionHost.CancelOutputRead();
                    functionHost.CancelErrorRead();
                    functionHost.Kill(true);
                    functionHost.Dispose();
                    functionHost.WaitForExit();
                }
                catch (Exception ex)
                {
                    this.LogOutput($"Failed to stop function host, Error: {ex.Message}");
                }
            }
            this.FunctionHostList.Clear();
        }

        protected async Task<HttpResponseMessage> SendInputRequest(string functionName, string query = "")
        {
            string requestUri = $"http://localhost:{this.Port}/api/{functionName}/{query}";

            return await this.SendGetRequest(requestUri);
        }

        protected Task<HttpResponseMessage> SendOutputGetRequest(string functionName, IDictionary<string, string> query = null)
        {
            string requestUri = $"http://localhost:{this.Port}/api/{functionName}";

            if (query != null)
            {
                requestUri = QueryHelpers.AddQueryString(requestUri, query);
            }

            return this.SendGetRequest(requestUri);
        }

        protected Task<HttpResponseMessage> SendOutputPostRequest(string functionName, string query)
        {
            string requestUri = $"http://localhost:{this.Port}/api/{functionName}";

            return this.SendPostRequest(requestUri, query);
        }

        protected void InsertProducts(Product[] products)
        {
            if (products.Length == 0)
            {
                return;
            }

            var queryBuilder = new StringBuilder();
            foreach (Product p in products)
            {
                queryBuilder.AppendLine($"INSERT INTO dbo.Products VALUES({p.ProductId}, '{p.Name}', {p.Cost});");
            }

            this.ExecuteNonQuery(queryBuilder.ToString(), $"Inserting {products.Length} products");
        }

        protected static Product[] GetProducts(int n, int cost)
        {
            var result = new Product[n];
            for (int i = 1; i <= n; i++)
            {
                result[i - 1] = new Product
                {
                    ProductId = i,
                    Name = "test",
                    Cost = cost * i
                };
            }
            return result;
        }

        protected static Product[] GetProductsWithSameCost(int n, int cost)
        {
            var result = new Product[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = new Product
                {
                    ProductId = i,
                    Name = "test",
                    Cost = cost
                };
            }
            return result;
        }

        protected static Product[] GetProductsWithSameCostAndName(int n, int cost, string name, int offset = 0)
        {
            var result = new Product[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = new Product
                {
                    ProductId = i + offset,
                    Name = name,
                    Cost = cost
                };
            }
            return result;
        }
    }
}