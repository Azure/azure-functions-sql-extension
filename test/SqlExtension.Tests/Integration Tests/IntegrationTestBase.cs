// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Data.SqlClient;
using System;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SqlExtension.Tests.Integration
{
    public class IntegrationTestBase : IDisposable
    {
        /// <summary>
        /// Host process for Azure Function CLI
        /// </summary>
        private Process FunctionHost;

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

            this.StartFunctionHost();
        }

        /// <remarks>
        /// Integration tests depend on a localhost server to be running.
        /// Either have one running locally with integrated auth, or start a mssql instance in a Docker container
        /// and set "SA_PASSWORD" as environment variable before running "dotnet tets".
        /// </remarks>
        private void SetupDatabase()
        {
            // First connect to master to create the database
            var connectionStringBuilder = new SqlConnectionStringBuilder()
            {
                DataSource = "localhost",
                InitialCatalog = "master",
                Pooling = false
            };

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
            // Ideally all the sql files would be in a sqlproj and can just be deployed
            string databaseScriptsPath = Path.Combine(this.GetPathToSamplesBin(), "Database");
            foreach (string file in Directory.EnumerateFiles(databaseScriptsPath, "*.sql"))
            {
                this.ExecuteNonQuery(File.ReadAllText(file));
            }

            // Set SqlConnectionString env var for the Function to use
            Environment.SetEnvironmentVariable("SqlConnectionString", connectionStringBuilder.ToString());
        }

        private void StartFunctionHost()
        {
            this.FunctionHost = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = this.GetFunctionsCoreToolsPath(),
                    Arguments = $"start --verbose --port {this.Port}",
                    WorkingDirectory = Path.Combine(this.GetPathToSamplesBin(), "SqlExtensionSamples"),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };
            this.FunctionHost.OutputDataReceived += this.TestOutputHandler;
            this.FunctionHost.ErrorDataReceived += this.TestOutputHandler;

            this.FunctionHost.Start();
            this.FunctionHost.BeginOutputReadLine();
            this.FunctionHost.BeginErrorReadLine();

            Thread.Sleep(5000);     // This is just to give some time to func host to start, maybe there's a better way to do this (check if port's open?)
        }

        private string GetFunctionsCoreToolsPath()
        {
            // Determine npm install path from either env var set by pipeline or OS defaults
            string modulesPath = Environment.GetEnvironmentVariable("node_modules_path");
            if (string.IsNullOrEmpty(modulesPath))
            {
                // Note that on Mac, we're looking for the default install location for brew packages, since that's what's recommended by the AF extension
                modulesPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"npm\node_modules\")
                    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? @"/usr/local/bin" : @"/usr/local/lib/node_modules";
            }

            string funcExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "func.exe" : "func";
            string funcPath = !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? Path.Combine(modulesPath, "azure-functions-core-tools", "bin", funcExe)
                : Path.Combine(modulesPath, funcExe);
            if (!File.Exists(funcPath))
            {
                throw new FileNotFoundException("Azure Function Core Tools not found at " + funcPath);
            }

            return funcPath;
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

        public string GetPathToSamplesBin()
        {
            return Path.GetDirectoryName(Assembly.GetAssembly(typeof(SqlExtensionSamples.Product)).Location);
        }

        public void Dispose()
        {
            this.Connection.Close();

            try
            {
                // Drop the test database
                using var masterConnection = new SqlConnection(this.MasterConnectionString);
                masterConnection.Open();
                TestUtils.ExecuteNonQuery(masterConnection, $"DROP DATABASE IF EXISTS {this.DatabaseName}");
            }
            catch (Exception e)
            {
                this.TestOutput.WriteLine($"Failed to drop {this.DatabaseName}, Error: {e.Message}");
            }
            finally
            {
                this.Connection.Dispose();

                this.FunctionHost.Kill();
                this.FunctionHost.Dispose();
            }
        }
    }
}
