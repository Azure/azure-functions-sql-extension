// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Data.SqlClient;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    /// <remarks>
    /// Adapted from Microsoft.VisualStudio.TeamSystem.Data.UnitTests.UnitTestUtilities.TestDBManager
    /// </remarks>
    public static class TestUtils
    {
        internal static int ThreadId;

        /// <summary>
        /// This returns a running Azurite storage emulator.
        /// </summary>
        public static Process StartAzurite()
        {
            Console.WriteLine("Starting Azurite Host...");
            var azuriteHost = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "azurite",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                }
            };

            azuriteHost.Start();
            return azuriteHost;
        }

        public static void StopAzurite(Process azuriteHost)
        {
            Console.WriteLine("Stopping Azurite Host...");
            try
            {
                azuriteHost.Kill(true);
                azuriteHost.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to stop Azurite, Error: {e.Message}");
            }
        }

        /// <summary>
        /// Sets up a test database for the tests to use.
        /// </summary>
        /// <remarks>
        /// The server the database will be created on can be set by the environment variable "TEST_SERVER", otherwise localhost will be used by default.
        /// By default, integrated authentication will be used to connect to the server, unless the env variable "SA_PASSWORD" is set.
        /// In this case, connection will be made using SQL login with user "SA" and the provided password.
        /// </remarks>
        public static void SetupDatabase(out string MasterConnectionString, out string DatabaseName)
        {
            SqlConnectionStringBuilder connectionStringBuilder;
            string connectionString = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING");
            string masterConnectionString;
            if (connectionString != null)
            {
                masterConnectionString = connectionString;
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
                masterConnectionString = connectionStringBuilder.ToString();
            }
            Console.WriteLine($"Setting up database on {connectionStringBuilder.DataSource} with {(connectionStringBuilder.IntegratedSecurity ? "Integrated Security" : "sa login")}");
            // Create database
            // Retry this in case the server isn't fully initialized yet
            string databaseName = GetUniqueDBName("SqlBindingsTest");
            Retry(() =>
            {
                using var masterConnection = new SqlConnection(masterConnectionString);
                masterConnection.Open();
                ExecuteNonQuery(masterConnection, $"CREATE DATABASE [{databaseName}]", Console.WriteLine);
                // Enable change tracking for trigger tests
                ExecuteNonQuery(masterConnection, $"ALTER DATABASE [{databaseName}] SET CHANGE_TRACKING = ON (CHANGE_RETENTION = 2 DAYS, AUTO_CLEANUP = ON);", Console.WriteLine);
            }, Console.WriteLine);

            connectionStringBuilder.InitialCatalog = databaseName;

            // Set SqlConnectionString and WEBSITE_SITE_NAME env variables for the tests to use
            Environment.SetEnvironmentVariable("SqlConnectionString", connectionStringBuilder.ToString());
            Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "TestSqlFunction");
            MasterConnectionString = masterConnectionString;
            DatabaseName = databaseName;
        }

        public static void DropDatabase(string masterConnectionString, string databaseName)
        {
            try
            {
                using var masterConnection = new SqlConnection(masterConnectionString);
                masterConnection.Open();
                ExecuteNonQuery(masterConnection, $"DROP DATABASE IF EXISTS {databaseName}", Console.WriteLine);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to drop {databaseName}, Error: {e.Message}");
            }
        }

        /// <summary>
        /// Returns a mangled name that unique based on Prefix + Machine + Process
        /// </summary>
        public static string GetUniqueDBName(string namePrefix)
        {
            string safeMachineName = Environment.MachineName.Replace('-', '_');
            return string.Format(
                "{0}_{1}_{2}_{3}_{4}",
                namePrefix,
                safeMachineName,
                AppDomain.CurrentDomain.Id,
                Environment.ProcessId,
                Interlocked.Increment(ref ThreadId));
        }

        /// <summary>
        /// Creates a IDbCommand and calls ExecuteNonQuery against the connection.
        /// </summary>
        /// <param name="connection">The connection.  This must be opened.</param>
        /// <param name="commandText">The scalar T-SQL command.</param>
        /// <param name="logger">The method to call for logging output</param>
        /// <param name="catchException">Optional exception handling.  Pass back 'true' to handle the
        /// exception, 'false' to throw. If Null is passed in then all exceptions are thrown.</param>
        /// <param name="message">Optional message to write when this query is executed. Defaults to writing the query commandText</param>
        /// <returns>The number of rows affected</returns>
        public static int ExecuteNonQuery(
            IDbConnection connection,
            string commandText,
            Action<string> logger,
            Predicate<Exception> catchException = null,
            string message = null)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }
            if (commandText == null)
            {
                throw new ArgumentNullException(nameof(commandText));
            }
            message ??= $"Executing non-query {commandText}";

            using IDbCommand cmd = connection.CreateCommand();
            try
            {

                cmd.CommandText = commandText;
                cmd.CommandType = CommandType.Text;
                cmd.CommandTimeout = 60; // Increase from default 30s to prevent timeouts while connecting to Azure SQL DB
                logger.Invoke(message);
                return cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                if (catchException == null || !catchException(ex))
                {
                    throw;
                }
            }

            return 0;
        }

        /// <summary>
        /// Creates a IDbCommand and calls ExecuteScalar against the connection.
        /// </summary>
        /// <param name="connection">The connection.  This must be opened.</param>
        /// <param name="commandText">The scalar T-SQL command.</param>
        /// <param name="logger">The method to call for logging output</param>
        /// <param name="catchException">Optional exception handling.  Pass back 'true' to handle the
        /// exception, 'false' to throw. If Null is passed in then all exceptions are thrown.</param>
        /// <returns>The scalar result</returns>
        public static object ExecuteScalar(
            IDbConnection connection,
            string commandText,
            Action<string> logger,
            Predicate<Exception> catchException = null)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }
            if (commandText == null)
            {
                throw new ArgumentNullException(nameof(commandText));
            }

            using IDbCommand cmd = connection.CreateCommand();
            try
            {
                cmd.CommandText = commandText;
                cmd.CommandType = CommandType.Text;
                logger.Invoke($"Executing scalar {commandText}");
                return cmd.ExecuteScalar();
            }
            catch (Exception ex)
            {
                if (catchException == null || !catchException(ex))
                {
                    throw;
                }
            }

            return null;
        }

        /// <summary>
        /// Retries the specified action, waiting for the specified duration in between each attempt
        /// </summary>
        /// <param name="action">The action to run</param>
        /// <param name="logger">The method to call for logging output</param>
        /// <param name="retryCount">The max number of retries to attempt</param>
        /// <param name="waitDurationMs">The duration in milliseconds between each attempt</param>
        /// <exception cref="AggregateException">Aggregate of all exceptions thrown if all retries failed</exception>
        public static void Retry(Action action, Action<string> logger, int retryCount = 3, int waitDurationMs = 10000)
        {
            var exceptions = new List<Exception>();
            while (true)
            {
                try
                {
                    action();
                    break;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    retryCount--;
                    if (retryCount == 0)
                    {
                        throw new AggregateException($"Action failed all retries", exceptions);
                    }
                    logger.Invoke($"Error running action, retrying after {waitDurationMs}ms. {retryCount} retries left. {ex}");
                    Thread.Sleep(waitDurationMs);
                }
            }
        }

        /// <summary>
        /// Removes spaces, tabs and new lines from the JSON string.
        /// </summary>
        /// <param name="jsonStr">The json string for trimming</param>
        public static string CleanJsonString(string jsonStr)
        {
            return jsonStr.Trim().Replace(" ", "").Replace(Environment.NewLine, "");
        }

        /// <summary>
        /// Returns a task that will complete when either the original task completes or the specified timeout is reached.
        /// </summary>
        /// <param name="task">The original task to wait on</param>
        /// <param name="timeout">The TimeSpan to wait for before a TimeoutException is thrown</param>
        /// <param name="message">The message to give the TimeoutException if a timeout occurs</param>
        /// <returns>A Task that will either complete once the original task completes or throw if the timeout period is reached, whichever occurs first</returns>
        /// <exception cref="TimeoutException">If the timeout is reached and the original Task hasn't completed</exception>
        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout, string message = "The operation has timed out.")
        {

            using var timeoutCancellationTokenSource = new CancellationTokenSource();

            Task completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
            if (completedTask == task)
            {
                timeoutCancellationTokenSource.Cancel();
                return await task;  // Very important in order to propagate exceptions
            }
            else
            {
                throw new TimeoutException(message);
            }
        }

        /// <summary>
        /// Returns a task that will complete when either the original task completes or the specified timeout is reached.
        /// </summary>
        /// <param name="task">The original task to wait on</param>
        /// <param name="timeout">The TimeSpan to wait for before a TimeoutException is thrown</param>
        /// <param name="message">The message to give the TimeoutException if a timeout occurs</param>
        /// <returns>A Task that will either complete once the original task completes or throw if the timeout period is reached, whichever occurs first</returns>
        /// <exception cref="TimeoutException">If the timeout is reached and the original Task hasn't completed</exception>
        public static async Task TimeoutAfter(this Task task, TimeSpan timeout, string message = "The operation has timed out.")
        {

            using var timeoutCancellationTokenSource = new CancellationTokenSource();

            Task completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
            if (completedTask == task)
            {
                timeoutCancellationTokenSource.Cancel();
                await task;  // Very important in order to propagate exceptions
            }
            else
            {
                throw new TimeoutException(message);
            }
        }

        /// <summary>
        /// Creates a DataReceievedEventHandler that will wait for the specified regex and then check that
        /// the matched group matches the expected value.
        /// </summary>
        /// <param name="taskCompletionSource">The task completion source to signal when the value is received</param>
        /// <param name="regex">The regex. This must have a single group match for the specific value being looked for</param>
        /// <param name="valueName">The name of the value to output if the match fails</param>
        /// <param name="expectedValue">The value expected to be equal to the matched group from the regex</param>
        /// <returns>The event handler</returns>
        public static DataReceivedEventHandler CreateOutputReceievedHandler(TaskCompletionSource<bool> taskCompletionSource, string regex, string valueName, string expectedValue)
        {
            return (object sender, DataReceivedEventArgs e) =>
            {
                if (e != null && e.Data != null)
                {
                    Match match = Regex.Match(e.Data, regex);
                    if (match.Success)
                    {
                        // We found the line so now check that the group matches our expected value
                        string actualValue = match.Groups[1].Value;
                        if (actualValue == expectedValue)
                        {
                            taskCompletionSource.SetResult(true);
                        }
                        else
                        {
                            taskCompletionSource.SetException(new Exception($"Expected {valueName} value of {expectedValue} but got value {actualValue}"));
                        }
                    }
                }
            };
        }

        public static string GetPathToBin()
        {
            return Path.GetDirectoryName(Assembly.GetAssembly(typeof(Product)).Location);
        }

        public static string GetFunctionsCoreToolsPath()
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

        // Set the default port to 7081 since the function hosts started in
        // IntegrationTestFixture are already running on ports 7071 - 7080.
        public const int DefaultPort = 7081;

        public static int GetPort(SupportedLanguages lang, bool testFolder = false)
        {
            if (lang == SupportedLanguages.CSharp && !testFolder)
            {
                return 7071;
            }
            else if (lang == SupportedLanguages.CSharp && testFolder)
            {
                return 7072;
            }
            else if (lang == SupportedLanguages.Csx)
            {
                return 7073;
            }
            else if (lang == SupportedLanguages.JavaScript)
            {
                return 7074;
            }
            else if (lang == SupportedLanguages.PowerShell)
            {
                return 7075;
            }
            else if (lang == SupportedLanguages.Java && !testFolder)
            {
                return 7076;
            }
            else if (lang == SupportedLanguages.Java && testFolder)
            {
                return 7077;
            }
            else if (lang == SupportedLanguages.OutOfProc && !testFolder)
            {
                return 7078;
            }
            else if (lang == SupportedLanguages.OutOfProc && testFolder)
            {
                return 7079;
            }
            else if (lang == SupportedLanguages.Python)
            {
                return 7080;
            }
            else
            {
                throw new Exception($"Failed to get port for language: {lang}");
            }
        }
    }
}
