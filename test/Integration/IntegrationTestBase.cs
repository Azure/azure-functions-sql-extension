// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Data.SqlClient;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using System;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public class IntegrationTestBase : IDisposable
    {
        /// <summary>
        /// Host process for Azure Function CLI
        /// </summary>
        private Process FunctionHost;

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
        /// Please use TestOutput.WriteLine() instead of Console or Debug.
        /// </summary>
        protected ITestOutputHelper TestOutput { get; private set; }

        /// <summary>
        /// The port the Functions Host is running on. Default is 7071.
        /// </summary>
        protected int Port { get; private set; } = 7071;

        public IntegrationTestBase(ITestOutputHelper output)
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
            // Get the test server name from environment variable "TEST_SERVER", default to localhost if not set
            string testServer = Environment.GetEnvironmentVariable("TEST_SERVER");
            if (string.IsNullOrEmpty(testServer))
            {
                testServer = "localhost";
            }

            // First connect to master to create the database
            var connectionStringBuilder = new SqlConnectionStringBuilder()
            {
                DataSource = testServer,
                InitialCatalog = "master",
                Pooling = false
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

            // Create database
            this.DatabaseName = TestUtils.GetUniqueDBName("SqlBindingsTest");
            using (var masterConnection = new SqlConnection(this.MasterConnectionString))
            {
                masterConnection.Open();
                TestUtils.ExecuteNonQuery(masterConnection, $"CREATE DATABASE [{this.DatabaseName}]");
            }

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
                Console.WriteLine($"Executing script ${file}");
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
        protected void StartFunctionHost(string functionName, bool useTestFolder = false)
        {
            string workingDirectory = useTestFolder ? GetPathToBin() : Path.Combine(GetPathToBin(), "SqlExtensionSamples");
            if (!Directory.Exists(workingDirectory))
            {
                throw new FileNotFoundException("Working directory not found at " + workingDirectory);
            }
            var startInfo = new ProcessStartInfo
            {
                // The full path to the Functions CLI is required in the ProcessStartInfo because UseShellExecute is set to false.
                // We cannot both use shell execute and redirect output at the same time: https://docs.microsoft.com//dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput#remarks
                FileName = GetFunctionsCoreToolsPath(),
                Arguments = $"start --verbose --port {this.Port} --functions {functionName}",
                WorkingDirectory = workingDirectory,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            this.TestOutput.WriteLine($"Starting {startInfo.FileName} {startInfo.Arguments} in {startInfo.WorkingDirectory}");
            this.FunctionHost = new Process
            {
                StartInfo = startInfo
            };
            this.FunctionHost.OutputDataReceived += this.TestOutputHandler;
            this.FunctionHost.ErrorDataReceived += this.TestOutputHandler;

            this.FunctionHost.Start();
            this.FunctionHost.BeginOutputReadLine();
            this.FunctionHost.BeginErrorReadLine();

            var taskCompletionSource = new TaskCompletionSource<bool>();
            this.FunctionHost.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                // This string is printed after the function host is started up - use this to ensure that we wait long enough
                // since sometimes the host can take a little while to fully start up
                if (e != null && !string.IsNullOrEmpty(e.Data) && e.Data.Contains($"http://localhost:{this.Port}/api"))
                {
                    taskCompletionSource.SetResult(true);
                }
            };
            this.TestOutput.WriteLine($"Waiting for Azure Function host to start...");
            taskCompletionSource.Task.Wait(60000);
            this.TestOutput.WriteLine($"Azure Function host started!");
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
                throw new FileNotFoundException("Azure Function Core Tools not found at " + funcPath);
            }

            return funcPath;
        }

        /// <summary>
        /// This starts the Javascript Functions runtime with the specified function(s).
        /// </summary>
        /// <remarks>
        /// - The functionName is different than its route.<br/>
        /// - You can start multiple functions by passing in a space-separated list of function names.<br/>
        /// </remarks>
        protected void StartJSFunctionHost(string functionName)
        {
            string jsSamplesDirectory = Path.Combine(GetPathToBin(), @"..\..\..\..\samples\samples-js");
            if (!Directory.Exists(jsSamplesDirectory))
            {
                throw new FileNotFoundException("Working directory not found at " + jsSamplesDirectory);
            }
            var startInfo = new ProcessStartInfo
            {
                // The full path to the Functions CLI is required in the ProcessStartInfo because UseShellExecute is set to false.
                // We cannot both use shell execute and redirect output at the same time: https://docs.microsoft.com//dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput#remarks
                FileName = GetFunctionsCoreToolsPath(),
                Arguments = $"start --verbose --port {this.Port} --functions {functionName}",
                WorkingDirectory = jsSamplesDirectory,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            this.TestOutput.WriteLine($"Starting {startInfo.FileName} {startInfo.Arguments} in {startInfo.WorkingDirectory}");
            this.FunctionHost = new Process
            {
                StartInfo = startInfo
            };
            this.FunctionHost.OutputDataReceived += this.TestOutputHandler;
            this.FunctionHost.ErrorDataReceived += this.TestOutputHandler;

            this.FunctionHost.Start();
            this.FunctionHost.BeginOutputReadLine();
            this.FunctionHost.BeginErrorReadLine();

            var taskCompletionSource = new TaskCompletionSource<bool>();
            this.FunctionHost.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                // This string is printed after the function host is started up - use this to ensure that we wait long enough
                // since sometimes the host can take a little while to fully start up
                if (e != null && !string.IsNullOrEmpty(e.Data) && e.Data.Contains($"http://localhost:{this.Port}/api"))
                {
                    taskCompletionSource.SetResult(true);
                }
            };
            this.TestOutput.WriteLine($"Waiting for Azure Function host to start...");
            taskCompletionSource.Task.Wait(60000);
            this.TestOutput.WriteLine($"Azure Function host started!");
        }

        private void TestOutputHandler(object sender, DataReceivedEventArgs e)
        {
            if (e != null && !string.IsNullOrEmpty(e.Data))
            {
                this.TestOutput.WriteLine(e.Data);
            }
        }

        protected async Task<HttpResponseMessage> SendGetRequest(string requestUri, bool verifySuccess = true)
        {
            string timeStamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
            this.TestOutput.WriteLine($"[{timeStamp}] Sending GET request: {requestUri}");

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
            this.TestOutput.WriteLine("Sending POST request: " + requestUri);

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
            try
            {
                this.Connection.Close();
            }
            catch (Exception e1)
            {
                this.TestOutput.WriteLine($"Failed to close connection. Error: {e1.Message}");
            }
            try
            {
                // Drop the test database
                using var masterConnection = new SqlConnection(this.MasterConnectionString);
                masterConnection.Open();
                TestUtils.ExecuteNonQuery(masterConnection, $"DROP DATABASE IF EXISTS {this.DatabaseName}");
            }
            catch (Exception e2)
            {
                this.TestOutput.WriteLine($"Failed to drop {this.DatabaseName}, Error: {e2.Message}");
            }
            finally
            {
                this.Connection.Dispose();

                // Try to clean up after test run, but don't consider it a failure if we can't for some reason
                try
                {
                    this.FunctionHost?.Kill();
                    this.FunctionHost?.Dispose();
                }
                catch (Exception e3)
                {
                    this.TestOutput.WriteLine($"Failed to stop function host, Error: {e3.Message}");
                }

                try
                {
                    this.AzuriteHost?.Kill();
                    this.AzuriteHost?.Dispose();
                }
                catch (Exception e4)
                {
                    this.TestOutput.WriteLine($"Failed to stop Azurite, Error: {e4.Message}");
                }
            }
            GC.SuppressFinalize(this);
        }
    }
}
