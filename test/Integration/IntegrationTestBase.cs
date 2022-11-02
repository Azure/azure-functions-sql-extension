// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
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
        /// Host process for Azurite local storage emulator. This is required for non-HTTP trigger functions:
        /// https://docs.microsoft.com/azure/azure-functions/functions-develop-local
        /// </summary>
        private Process AzuriteHost;

        /// <summary>
        /// Connection to the database for the current test.
        /// </summary>
        private DbConnection Connection;

        /// <summary>
        /// Connection string to the master database on the test server, mainly used for database setup and teardown.
        /// </summary>
        private string MasterConnectionString;

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
            this.SetupDatabase();
            this.StartAzurite();
        }

        /// <summary>
        /// Sets up a test database for the current test to use.
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

            // Setup connection
            connectionStringBuilder.InitialCatalog = this.DatabaseName;
            this.Connection = new SqlConnection(connectionStringBuilder.ToString());
            this.Connection.Open();

            // Create the database definition
            // Create these in a specific order since things like views require that their underlying objects have been created already
            // Ideally all the sql files would be in a sqlproj and can just be deployed
            this.ExecuteAllScriptsInFolder(Path.Combine(GetPathToBin(), "Database", "Tables"));
            this.ExecuteAllScriptsInFolder(Path.Combine(GetPathToBin(), "Database", "Views"));
            this.ExecuteAllScriptsInFolder(Path.Combine(GetPathToBin(), "Database", "StoredProcedures"));

            // Set SqlConnectionString env var for the Function to use
            Environment.SetEnvironmentVariable("SqlConnectionString", connectionStringBuilder.ToString());
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
        /// This starts the Azurite storage emulator.
        /// </summary>
        protected void StartAzurite()
        {
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
        /// This starts the Functions runtime with the specified function(s).
        /// </summary>
        /// <remarks>
        /// - The functionName is different than its route.<br/>
        /// - You can start multiple functions by passing in a space-separated list of function names.<br/>
        /// </remarks>
        protected void StartFunctionHost(string functionName, SupportedLanguages language, bool useTestFolder = false, DataReceivedEventHandler customOutputHandler = null, IDictionary<string, string> environmentVariables = null)
        {
            string workingDirectory = language == SupportedLanguages.CSharp && useTestFolder ? GetPathToBin() : Path.Combine(GetPathToBin(), "SqlExtensionSamples", Enum.GetName(typeof(SupportedLanguages), language));
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
            if (environmentVariables != null)
            {
                environmentVariables.ToList().ForEach(ev => startInfo.EnvironmentVariables[ev.Key] = ev.Value);
            }

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
            functionHost.OutputDataReceived += SignalStartupHandler;
            this.FunctionHost.OutputDataReceived += customOutputHandler;

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
            this.FunctionHost.OutputDataReceived -= SignalStartupHandler;

            void SignalStartupHandler(object sender, DataReceivedEventArgs e)
            {
                // This string is printed after the function host is started up - use this to ensure that we wait long enough
                // since sometimes the host can take a little while to fully start up
                if (e.Data?.Contains(" Host initialized ") == true)
                {
                    taskCompletionSource.SetResult(true);
                }
            };
            taskCompletionSource.Task.Wait(60000);
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
                throw new FileNotFoundException($"Azure Function Core Tools not found at {funcPath}");
            }

            return funcPath;
        }

        private void LogOutput(string output)
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
            return TestOutputHandler;

            void TestOutputHandler(object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    this.LogOutput($"[{processId}] {e.Data}");
                }
            }
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
        protected void ExecuteNonQuery(string commandText)
        {
            TestUtils.ExecuteNonQuery(this.Connection, commandText);
        }

        /// <summary>
        /// Executes a command against the current connection and the result is returned.
        /// </summary>
        protected object ExecuteScalar(string commandText)
        {
            return TestUtils.ExecuteScalar(this.Connection, commandText);
        }

        private static string GetPathToBin()
        {
            return Path.GetDirectoryName(Assembly.GetAssembly(typeof(Product)).Location);
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

            foreach (Process functionHost in this.FunctionHostList)
            {
                try
                {
                    functionHost.Kill();
                    functionHost.Dispose();
                }
                catch (Exception e2)
                {
                    this.LogOutput($"Failed to stop function host, Error: {e2.Message}");
                }
            }

            try
            {
                this.AzuriteHost?.Kill();
                this.AzuriteHost?.Dispose();
            }
            catch (Exception e3)
            {
                this.LogOutput($"Failed to stop Azurite, Error: {e3.Message}");
            }

            try
            {
                // Drop the test database
                using var masterConnection = new SqlConnection(this.MasterConnectionString);
                masterConnection.Open();
                TestUtils.ExecuteNonQuery(masterConnection, $"DROP DATABASE IF EXISTS {this.DatabaseName}");
            }
            catch (Exception e4)
            {
                this.LogOutput($"Failed to drop {this.DatabaseName}, Error: {e4.Message}");
            }

            GC.SuppressFinalize(this);
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
                queryBuilder.AppendLine($"INSERT INTO dbo.Products VALUES({p.ProductID}, '{p.Name}', {p.Cost});");
            }

            this.ExecuteNonQuery(queryBuilder.ToString());
        }

        protected static Product[] GetProducts(int n, int cost)
        {
            var result = new Product[n];
            for (int i = 1; i <= n; i++)
            {
                result[i - 1] = new Product
                {
                    ProductID = i,
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
                    ProductID = i,
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
                    ProductID = i + offset,
                    Name = name,
                    Cost = cost
                };
            }
            return result;
        }
    }
}
