// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Microsoft.Data.SqlClient;

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

        /// <summary>
        /// Connection string to the master database on the test server, mainly used for database setup and teardown.
        /// </summary>
        private string MasterConnectionString;

        /// <summary>
        /// Name of the database used.
        /// </summary>
        private string DatabaseName;

        public IntegrationTestFixture()
        {
            this.StartAzurite();
            this.SetupDatabase();
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
                TestUtils.ExecuteNonQuery(masterConnection, $"CREATE DATABASE [{this.DatabaseName}]", this.LogOutput);
            }, this.LogOutput);

            connectionStringBuilder.InitialCatalog = this.DatabaseName;

            // Set SqlConnectionString env var for the tests to use
            Environment.SetEnvironmentVariable("SqlConnectionString", connectionStringBuilder.ToString());
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

            try
            {
                // Drop the test database
                using var masterConnection = new SqlConnection(this.MasterConnectionString);
                masterConnection.Open();
                TestUtils.ExecuteNonQuery(masterConnection, $"DROP DATABASE IF EXISTS {this.DatabaseName}", this.LogOutput);
            }
            catch (Exception e4)
            {
                Console.WriteLine($"Failed to drop {this.DatabaseName}, Error: {e4.Message}");
            }

            GC.SuppressFinalize(this);
        }

        private void LogOutput(string output)
        {
            Console.WriteLine(output);
        }
    }
}
