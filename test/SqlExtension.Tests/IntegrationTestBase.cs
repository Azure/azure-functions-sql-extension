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

namespace SqlExtension.Tests
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
        /// Name of the database used for the current test.
        /// </summary>
        protected string DatabaseName { get; private set; }

        /// <summary>
        /// Output redirect for XUnit tests.
        /// Please use TestOutput.WriteLine() instead of Console or Debug.
        /// </summary>
        protected ITestOutputHelper TestOutput { get; private set; }

        protected int Port { get; private set; }

        public IntegrationTestBase(ITestOutputHelper output)
        {
            TestOutput = output;

            SetupDatabase();
            
            StartFunctionHost();
        }

        /// <remarks>
        /// Integration tests depend on a localhost server to be running.
        /// Either have one running locally with integrated auth, or start a mssql instance in a Docker container 
        /// and set "SA_PASSWORD" as environment variable before running "dotnet tets".
        /// </remarks>
        private void SetupDatabase()
        {
            // First connect to master to create the database
            SqlConnectionStringBuilder connectionStringBuilder = new SqlConnectionStringBuilder()
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

            // Create database
            DatabaseName = TestUtils.GetUniqueDBName("SqlBindingsTest");
            using (SqlConnection masterConnection = new SqlConnection(connectionStringBuilder.ToString()))
            {
                masterConnection.Open();
                TestUtils.ExecuteNonQuery(masterConnection, $"CREATE DATABASE [{DatabaseName}]");
            }

            // Setup connection
            connectionStringBuilder.InitialCatalog = DatabaseName;
            Connection = new SqlConnection(connectionStringBuilder.ToString());
            Connection.Open();

            // Create the database definition
            // Ideally all the sql files would be in a sqlproj and can just be deployed
            string databaseScriptsPath = Path.Combine(GetPathToSamplesBin(), "Database");
            foreach (string file in Directory.EnumerateFiles(databaseScriptsPath, "*.sql"))
            {
                ExecuteNonQuery(File.ReadAllText(file));
            }

            // Set SqlConnectionString env var for the Function to use
            Environment.SetEnvironmentVariable("SqlConnectionString", connectionStringBuilder.ToString());
        }

        private void StartFunctionHost()
        {
            Port = 7071 + TestUtils.ThreadId;

            FunctionHost = new Process();
            FunctionHost.StartInfo = new ProcessStartInfo
            {
                FileName = GetFunctionsCoreToolsPath(),
                Arguments = $"start --verbose --port {Port}",
                WorkingDirectory = Path.Combine(GetPathToSamplesBin(), "SqlExtensionSamples"),
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            FunctionHost.OutputDataReceived += TestOutputHandler;
            FunctionHost.ErrorDataReceived += TestOutputHandler;

            FunctionHost.Start();
            FunctionHost.BeginOutputReadLine();
            FunctionHost.BeginErrorReadLine();

            Thread.Sleep(5000);     // This is just to give some time to func host to start, maybe there's a better way to do this (check if port's open?)
        }

        private string GetFunctionsCoreToolsPath()
        {
            // Determine npm install path from either env var set by pipeline or OS defaults
            string nodeModulesPath = Environment.GetEnvironmentVariable("node_modules_path");
            if (string.IsNullOrEmpty(nodeModulesPath))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    nodeModulesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"npm\node_modules\");
                }
                else
                {
                    nodeModulesPath = @"/usr/local/lib/node_modules";
                }
            }

            string funcExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "func.exe" : "func";
            string funcPath = Path.Combine(nodeModulesPath, "azure-functions-core-tools", "bin", funcExe);

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
                TestOutput.WriteLine(e.Data);
            }
        }

        protected async Task<HttpResponseMessage> SendGetRequest(string requestUri, bool verifySuccess = true)
        {
            string timeStamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
            TestOutput.WriteLine($"[{timeStamp}] Sending GET request: {requestUri}");

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

        protected async Task<HttpResponseMessage> SendPostRequest(string requestUri, string json, bool verifySuccess = true)
        {
            TestOutput.WriteLine("Sending POST request: " + requestUri);

            if (string.IsNullOrEmpty(requestUri))
            {
                throw new ArgumentException("URI cannot be null or empty.");
            }

            HttpClient client = new HttpClient();
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
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
            TestUtils.ExecuteNonQuery(Connection, commandText);
        }

        /// <summary>
        /// Executes a command against the current connection and the result is returned.
        /// </summary>
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

                FunctionHost.Kill();
                FunctionHost.Dispose();
            }
        }
    }
}
