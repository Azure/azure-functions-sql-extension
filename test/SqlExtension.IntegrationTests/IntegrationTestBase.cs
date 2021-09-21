using Microsoft.Data.SqlClient;
using System;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SqlExtension.IntegrationTests
{
    public class IntegrationTestBase : IDisposable
    {
        /// <summary>
        /// Process object for Azure Function CLI
        /// </summary>
        private Process FunctionHost;

        /// <summary>
        /// Connection to the database for the current test.
        /// </summary>
        private DbConnection Connection;

        /// <summary>
        /// Name of the database used for the current test.
        /// </summary>
        protected string DatabaseName { get; private set; }

        /// <summary>
        /// Output redirect for XUnit tests. Please use TestOutput.WriteLine() instead of Console or Debug.
        /// </summary>
        protected ITestOutputHelper TestOutput { get; private set; }

        public IntegrationTestBase(ITestOutputHelper output)
        {
            TestOutput = output;

            SetupDatabase();

            StartFunctionHost();
        }

        private void SetupDatabase()
        {
            // Create the test database
            DatabaseName = TestUtils.GetUniqueDBName("SqlBindingsTest");
            using (SqlConnection masterConnection = new SqlConnection("Data Source=(LocalDb)\\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True;Pooling=False;"))
            {
                masterConnection.Open();
                TestUtils.ExecuteNonQuery(masterConnection, $"CREATE DATABASE [{DatabaseName}]");
            }

            // Setup connection
            string connectionString = $"Data Source=(LocalDb)\\MSSQLLocalDB;Initial Catalog={DatabaseName};Integrated Security=True;Pooling=False;";
            Connection = new SqlConnection(connectionString);
            Connection.Open();

            // Create the database definition
            string databaseScriptsPath = Path.Combine(GetPathToSamplesBin(), "Database");
            foreach (string file in Directory.EnumerateFiles(databaseScriptsPath, "*.sql"))
            {
                ExecuteNonQuery(File.ReadAllText(file));
            }

            // Set SqlConnectionString env var for the Function to use
            Environment.SetEnvironmentVariable("SqlConnectionString", connectionString);
        }

        private void StartFunctionHost()
        {
            string funcExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "func.exe" : "func";
            string funcPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"npm\node_modules\azure-functions-core-tools\bin", funcExe);

            if (!File.Exists(funcPath))
            {
                throw new FileNotFoundException("Azure Function Core Tools not found at " + funcPath);
            }

            FunctionHost = new Process();
            FunctionHost.StartInfo = new ProcessStartInfo
            {
                FileName = funcPath,
                Arguments = "start --verbose",
                WorkingDirectory = Path.Combine(GetPathToSamplesBin(), "SqlExtensionSamples"),
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            FunctionHost.OutputDataReceived += TestOutputHandler;
            FunctionHost.ErrorDataReceived += TestErrorOutputHandler;

            FunctionHost.Start();
            FunctionHost.BeginOutputReadLine();
            FunctionHost.BeginErrorReadLine();

            Thread.Sleep(5000);     // This is just to give some time to func host to start, maybe there's a better way to do this (check if port's open?)
        }

        private void TestOutputHandler(object sender, DataReceivedEventArgs e)
        {
            TestOutput.WriteLine(e.Data);
        }

        private void TestErrorOutputHandler(object sender, DataReceivedEventArgs e)
        {
            throw new Exception("Function host failed with error: " + e.Data);
        }

        protected async Task<HttpResponseMessage> SendGetRequest(string requestUri, bool verifySuccess = true)
        {
            TestOutput.WriteLine("Sending GET request: " + requestUri);

            if (string.IsNullOrEmpty(requestUri))
            {
                throw new ArgumentException("URI cannot be null or empty.");
            }

            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(requestUri);

            if (verifySuccess)
            {
                Assert.True(response.IsSuccessStatusCode, $"Http request failed with code {response.StatusCode}. Please check output for more detailed message.");
            }

            return response;
        }

        protected void ExecuteNonQuery(string commandText)
        {
            TestUtils.ExecuteNonQuery(Connection, commandText);
        }

        protected object ExecuteScalar(string commandText)
        {
            return TestUtils.ExecuteScalar(Connection, commandText);
        }

        public string GetPathToSamplesBin()
        {
            return Path.GetDirectoryName(Assembly.GetAssembly(typeof(SqlExtensionSamples.Product)).Location);
        }

        public void Dispose()
        {
            Connection.Close();

            try
            {
                // Drop the test database
                using (SqlConnection masterConnection = new SqlConnection("Data Source=(LocalDb)\\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True;Pooling=False;"))
                {
                    masterConnection.Open();
                    TestUtils.ExecuteNonQuery(masterConnection, $"DROP DATABASE IF EXISTS {DatabaseName}");
                }
            }
            catch (Exception e)
            {
                TestOutput.WriteLine($"Failed to drop {DatabaseName}, Error: {e.Message}");
            }
            finally
            {
                Connection.Dispose();

                if (!FunctionHost.HasExited)
                {
                    FunctionHost.Kill();
                }

                FunctionHost.CloseMainWindow();
                FunctionHost.Dispose();
            }
        }
    }
}
