﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using static Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry.Telemetry;
using Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal static class SqlBindingUtilities
    {
        /// <summary>
        /// Builds a connection using the connection string attached to the app setting with name ConnectionStringSetting
        /// </summary>
        /// <param name="connectionStringSetting">The name of the app setting that stores the SQL connection string</param>
        /// <param name="configuration">Used to obtain the value of the app setting</param>
        /// <exception cref="ArgumentException">
        /// Thrown if ConnectionStringSetting is empty or null
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if configuration is null
        /// </exception>
        /// <returns>The built connection </returns>
        public static SqlConnection BuildConnection(string connectionStringSetting, IConfiguration configuration)
        {
            return new SqlConnection(GetConnectionString(connectionStringSetting, configuration));
        }

        public static string GetConnectionString(string connectionStringSetting, IConfiguration configuration)
        {
            if (string.IsNullOrEmpty(connectionStringSetting))
            {
                throw new ArgumentException("Must specify ConnectionStringSetting, which should refer to the name of an app setting that " +
                    "contains a SQL connection string");
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            string connectionString = configuration.GetConnectionStringOrSetting(connectionStringSetting);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException(connectionString == null ? $"ConnectionStringSetting '{connectionStringSetting}' is missing in your function app settings, please add the setting with a valid SQL connection string." :
                $"ConnectionStringSetting '{connectionStringSetting}' is empty in your function app settings, please update the setting with a valid SQL connection string.");
            }
            return connectionString;
        }

        internal static readonly char[] ParameterSeparator = new[] { ',' };

        /// <summary>
        /// Parses the parameter string into a list of parameters, where each parameter is separated by "," and has the form
        /// "@param1=param2". "@param1" is the parameter name to be used in the query or stored procedure, and param1 is the
        /// parameter value. Parameter name and parameter value are separated by "=". Parameter names/values cannot contain ',' or '='.
        /// A valid parameter string would be "@param1=param1,@param2=param2". Attaches each parsed parameter to command.
        /// If the value of a parameter should be null, use "null", as in @param1=null,@param2=param2".
        /// If the value of a parameter should be an empty string, do not add anything after the equals sign and before the comma,
        /// as in "@param1=,@param2=param2"
        /// </summary>
        /// <param name="parameters">The parameter string to be parsed</param>
        /// <param name="command">The SqlCommand to which the parsed parameters will be added to</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if command is null
        /// </exception>
        public static void ParseParameters(string parameters, SqlCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            // If parameters is null, user did not specify any parameters in their function so nothing to parse
            if (!string.IsNullOrEmpty(parameters))
            {
                // Because we remove empty entries, we will ignore any commas that appear at the beginning/end of the parameter list,
                // as well as extra commas that appear between parameter pairs.
                // I.e., ",,@param1=param1,,@param2=param2,,," will be parsed just like "@param1=param1,@param2=param2" is.
                string[] paramPairs = parameters.Split(ParameterSeparator, StringSplitOptions.RemoveEmptyEntries);

                foreach (string pair in paramPairs)
                {
                    // Note that we don't throw away empty entries here, so a parameter pair that looks like "=@param1=param1"
                    // or "@param2=param2=" is considered malformed
                    string[] items = pair.Split('=');
                    if (items.Length != 2)
                    {
                        throw new ArgumentException("Parameters must be separated by \",\" and parameter name and parameter value must be separated by \"=\", " +
                           "i.e. \"@param1=param1,@param2=param2\". To specify a null value, use null, as in \"@param1=null,@param2=param2\"." +
                           "To specify an empty string as a value, simply do not add anything after the equals sign, as in \"@param1=,@param2=param2\".");
                    }
                    if (!items[0].StartsWith("@", StringComparison.InvariantCultureIgnoreCase))
                    {
                        throw new ArgumentException("Parameter name must start with \"@\", i.e. \"@param1=param1,@param2=param2\"");
                    }


                    if (items[1].Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        command.Parameters.Add(new SqlParameter(items[0], DBNull.Value));
                    }
                    else
                    {
                        command.Parameters.Add(new SqlParameter(items[0], items[1]));
                    }
                }
            }
        }

        /// <summary>
        /// Builds a SqlCommand using the query/stored procedure and parameters specified in attribute.
        /// </summary>
        /// <param name="attribute">The SqlAttribute with the parameter, command type, and command text</param>
        /// <param name="connection">The connection to attach to the SqlCommand</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the CommandType specified in attribute is neither StoredProcedure nor Text. We only support
        /// commands that refer to the name of a StoredProcedure (the StoredProcedure CommandType) or are themselves
        /// raw queries (the Text CommandType).
        /// </exception>
        /// <returns>The built SqlCommand</returns>
        public static SqlCommand BuildCommand(SqlAttribute attribute, SqlConnection connection)
        {
            var command = new SqlCommand
            {
                Connection = connection,
                CommandText = attribute.CommandText
            };
            if (attribute.CommandType == CommandType.StoredProcedure)
            {
                command.CommandType = CommandType.StoredProcedure;
            }
            else if (attribute.CommandType != CommandType.Text)
            {
                throw new ArgumentException("The type of the SQL attribute for an input binding must be either CommandType.Text for a direct SQL query, or CommandType.StoredProcedure for a stored procedure.");
            }
            ParseParameters(attribute.Parameters, command);
            return command;
        }

        /// <summary>
        /// Returns a dictionary where each key is a column name and each value is the SQL row's value for that column
        /// </summary>
        /// <param name="reader">Used to determine the columns of the table as well as the next SQL row to process</param>
        /// <returns>The built dictionary</returns>
        public static IReadOnlyDictionary<string, object> BuildDictionaryFromSqlRow(SqlDataReader reader)
        {
            return Enumerable.Range(0, reader.FieldCount).ToDictionary(reader.GetName, i => reader.GetValue(i));
        }

        /// <summary>
        /// Escape any existing closing brackets and add brackets around the string
        /// </summary>
        /// <param name="s">The string to bracket quote.</param>
        /// <returns>The escaped and bracket quoted string.</returns>
        public static string AsBracketQuotedString(this string s)
        {
            return $"[{s.Replace("]", "]]")}]";
        }

        /// <summary>
        /// Escape any existing quotes and add quotes around the string.
        /// </summary>
        /// <param name="s">The string to quote.</param>
        /// <returns>The escaped and quoted string.</returns>
        public static string AsSingleQuotedString(this string s)
        {
            return $"'{s.AsSingleQuoteEscapedString()}'";
        }

        /// <summary>
        /// Returns the string with any single quotes in it escaped (replaced with '')
        /// </summary>
        /// <param name="s">The string to escape.</param>
        /// <returns>The escaped string.</returns>
        public static string AsSingleQuoteEscapedString(this string s)
        {
            return s.Replace("'", "''");
        }

        /// <summary>
        /// Verifies that the database we're connected to is supported
        /// </summary>
        /// <exception cref="InvalidOperationException">Throw if an error occurs while querying the compatibility level or if the database is not supported</exception>
        public static async Task VerifyDatabaseSupported(SqlConnection connection, ILogger logger, CancellationToken cancellationToken)
        {
            // Need at least 130 for OPENJSON support
            const int MIN_SUPPORTED_COMPAT_LEVEL = 130;

            string verifyDatabaseSupportedQuery = $"SELECT compatibility_level FROM sys.databases WHERE Name = DB_NAME()";

            using (var verifyDatabaseSupportedCommand = new SqlCommand(verifyDatabaseSupportedQuery, connection))
            using (SqlDataReader reader = verifyDatabaseSupportedCommand.ExecuteReaderWithLogging(logger))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException($"Received empty response when verifying whether the database is currently supported.");
                }

                int compatLevel = reader.GetByte(0);

                if (compatLevel < MIN_SUPPORTED_COMPAT_LEVEL)
                {
                    throw new InvalidOperationException($"SQL bindings require a database compatibility level of 130 or higher to function. Current compatibility level = {compatLevel}");
                }
            }
        }

        /// <summary>
        /// Opens a connection and handles some specific errors if they occur.
        /// </summary>
        /// <param name="connection">The connection to open</param>
        /// <param name="cancellationToken">The cancellation token to pass to the OpenAsync call</param>
        /// <returns>The task that will be completed when the connection is made</returns>
        /// <exception cref="InvalidOperationException">Thrown if an error occurred that we want to wrap with more information</exception>
        internal static async Task OpenAsyncWithSqlErrorHandling(this SqlConnection connection, CancellationToken cancellationToken)
        {
            try
            {
                await connection.OpenAsync(cancellationToken);
            }
            catch (Exception e)
            {
                SqlException sqlEx = e is AggregateException a ? a.InnerExceptions.OfType<SqlException>().First() :
                    e is SqlException s ? s :
                    null;
                // Error number for:
                //  A connection was successfully established with the server, but then an error occurred during the login process.
                //  The certificate chain was issued by an authority that is not trusted.
                // Add on some more information to help the user figure out how to solve it
                if (sqlEx?.Number == -2146893019)
                {
                    throw new InvalidOperationException("The default values for encryption on connections have been changed, please review your configuration to ensure you have the correct values for your server. See https://aka.ms/afsqlext-connection for more details.", e);
                }
                throw;
            }
        }

        /// <summary>
        /// Checks whether an exception is a fatal SqlException. It is determined to be fatal
        /// if the Class value of the Exception is 20 or higher, see
        /// https://learn.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlexception#remarks
        /// for details
        /// </summary>
        /// <param name="e">The exception to check</param>
        /// <returns>True if the exception is a fatal SqlClientException, false otherwise</returns>
        internal static bool IsFatalSqlException(this Exception e)
        {
            string lowerMessage = e.Message.ToLowerInvariant();
            // Most SqlExceptions wrap the original error from the native driver, so make sure to check both
            return (e as SqlException)?.Class >= 20
                || (e.InnerException as SqlException)?.Class >= 20
                // TEMPORARY - Not all exceptions thrown by SqlClient are SqlExceptions, and the current SqlClient
                // does not correctly update the State property in all cases. So for now explicitly check for
                // the string containing a message indicating that the connection has been closed or broken.
                // This should be removed once version 5.2.0+ has been released
                // https://github.com/Azure/azure-functions-sql-extension/issues/860
                || (lowerMessage.Contains("connection") && (lowerMessage.Contains("broken") || lowerMessage.Contains("closed")));
        }

        /// <summary>
        /// Whether the exception is a SqlClient deadlock exception (Error Number 1205)
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        internal static bool IsDeadlockException(this Exception e)
        {
            // See https://learn.microsoft.com/sql/relational-databases/errors-events/mssqlserver-1205-database-engine-error
            // Transaction (Process ID %d) was deadlocked on %.*ls resources with another process and has been chosen as the deadlock victim. Rerun the transaction.
            return (e as SqlException)?.Number == 1205
                || (e.InnerException as SqlException)?.Number == 1205;
        }

        /// <summary>
        /// Checks whether the connection state is currently Broken or Closed
        /// </summary>
        /// <param name="conn">The connection to check</param>
        /// <returns>True if the connection is broken or closed, false otherwise</returns>
        internal static bool IsBrokenOrClosed(this SqlConnection conn)
        {
            return conn.State == ConnectionState.Broken || conn.State == ConnectionState.Closed;
        }

        /// <summary>
        /// Attempts to ensure that this connection is open, if it currently is in a broken state
        /// then it will close the connection and re-open it.
        /// </summary>
        /// <param name="conn">The connection</param>
        /// <param name="forceReconnect">Whether to force the connection to be re-established, regardless of its current state</param>
        /// <param name="logger">Logger to log events to</param>
        /// <param name="connectionName">The name of the connection to display in the log messages</param>
        /// <param name="token">Cancellation token to pass to the Open call</param>
        /// <returns>True if the connection is open, either because it was able to be re-established or because it was already open. False if the connection could not be re-established.</returns>
        internal static async Task<bool> TryEnsureConnected(this SqlConnection conn,
            bool forceReconnect,
            ILogger logger,
            string connectionName,
            CancellationToken token)
        {
            if (forceReconnect || conn.IsBrokenOrClosed())
            {
                logger.LogWarning($"{connectionName} is broken, attempting to reconnect...");
                try
                {
                    // Sometimes the connection state is listed as open even if a fatal exception occurred, see
                    // https://github.com/dotnet/SqlClient/issues/1874 for details. So in that case we want to first
                    // close the connection so we can retry (otherwise it'll throw saying the connection is still open)
                    if (conn.State == ConnectionState.Open)
                    {
                        conn.Close();
                    }
                    await conn.OpenAsync(token);
                    logger.LogInformation($"Successfully re-established {connectionName}!");
                    return true;
                }
                catch (Exception e)
                {
                    logger.LogError($"Exception reconnecting {connectionName}. Exception = {e.Message}");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Get the Server Properties for the given connection.
        /// </summary>
        /// <returns>ServerProperties (EngineEdition and Edition) of the target Sql Server.</returns>
        public static async Task<ServerProperties> GetServerTelemetryProperties(SqlConnection connection, ILogger logger, CancellationToken cancellationToken)
        {
            if (TelemetryInstance.Enabled)
            {
                try
                {
                    string serverPropertiesQuery = $"SELECT SERVERPROPERTY('EngineEdition'), SERVERPROPERTY('Edition')";

                    using (var selectServerEditionCommand = new SqlCommand(serverPropertiesQuery, connection))
                    using (SqlDataReader reader = selectServerEditionCommand.ExecuteReaderWithLogging(logger))
                    {
                        if (await reader.ReadAsync(cancellationToken))
                        {
                            int engineEdition = reader.GetInt32(0);
                            var serverProperties = new ServerProperties() { Edition = reader.GetString(1) };
                            // Mapping information from
                            // https://learn.microsoft.com/en-us/sql/t-sql/functions/serverproperty-transact-sql?view=sql-server-ver16
                            switch (engineEdition)
                            {
                                case 1:
                                    serverProperties.EngineEdition = SqlBindingConstants.EngineEdition.DesktopEngine.ToString();
                                    return serverProperties;
                                case 2:
                                    serverProperties.EngineEdition = SqlBindingConstants.EngineEdition.Standard.ToString();
                                    return serverProperties;
                                case 3:
                                    serverProperties.EngineEdition = SqlBindingConstants.EngineEdition.Enterprise.ToString();
                                    return serverProperties;
                                case 4:
                                    serverProperties.EngineEdition = SqlBindingConstants.EngineEdition.Express.ToString();
                                    return serverProperties;
                                case 5:
                                    serverProperties.EngineEdition = SqlBindingConstants.EngineEdition.SQLDatabase.ToString();
                                    return serverProperties;
                                case 6:
                                    serverProperties.EngineEdition = SqlBindingConstants.EngineEdition.AzureSynapseAnalytics.ToString();
                                    return serverProperties;
                                case 8:
                                    serverProperties.EngineEdition = SqlBindingConstants.EngineEdition.AzureSQLManagedInstance.ToString();
                                    return serverProperties;
                                case 9:
                                    serverProperties.EngineEdition = SqlBindingConstants.EngineEdition.AzureSQLEdge.ToString();
                                    return serverProperties;
                                case 11:
                                    serverProperties.EngineEdition = SqlBindingConstants.EngineEdition.AzureSynapseserverlessSQLpool.ToString();
                                    return serverProperties;
                                default:
                                    serverProperties.EngineEdition = engineEdition.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                    return serverProperties;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"Exception in GetServerTelemetryProperties. Exception = {ex.Message}");
                    TelemetryInstance.TrackException(TelemetryErrorName.GetServerTelemetryProperties, ex);
                    return null;
                }
            }
            return null;
        }

        /// <summary>
        /// Calls ExecuteScalarAsync and logs an error if it fails before rethrowing.
        /// </summary>
        /// <param name="cmd">The SqlCommand being executed</param>
        /// <param name="logger">The logger</param>
        /// <param name="cancellationToken">The cancellation token to pass to the call</param>
        /// <param name="logCommand">Defaults to false and when set logs the command being executed</param>
        /// <returns>The result of the call</returns>
        public static async Task<object> ExecuteScalarAsyncWithLogging(this SqlCommand cmd, ILogger logger, CancellationToken cancellationToken, bool logCommand = false)
        {
            try
            {
                if (logCommand)
                {
                    logger.LogDebug($"Executing query={cmd.CommandText}");
                }
                return await cmd.ExecuteScalarAsync(cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError($"Exception executing query. Message={e.Message}\nQuery={cmd.CommandText}");
                throw;
            }
        }

        /// <summary>
        /// Calls ExecuteNonQueryAsync and logs an error if it fails before rethrowing.
        /// </summary>
        /// <param name="cmd">The SqlCommand being executed</param>
        /// <param name="logger">The logger</param>
        /// <param name="cancellationToken">The cancellation token to pass to the call</param>
        /// <param name="logCommand">Defaults to false and when set logs the command being executed</param>
        /// <returns>The result of the call</returns>
        public static async Task<int> ExecuteNonQueryAsyncWithLogging(this SqlCommand cmd, ILogger logger, CancellationToken cancellationToken, bool logCommand = false)
        {
            try
            {
                if (logCommand)
                {
                    logger.LogDebug($"Executing query={cmd.CommandText}");
                }
                return await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError($"Exception executing query. Message={e.Message}\nQuery={cmd.CommandText}");
                throw;
            }
        }

        /// <summary>
        /// Calls ExecuteReader and logs an error if it fails before rethrowing.
        /// </summary>
        /// <param name="cmd">The SqlCommand being executed</param>
        /// <param name="logger">The logger</param>
        /// <param name="logCommand">Defaults to false and when set logs the command being executed</param>
        /// <returns>The result of the call</returns>
        public static SqlDataReader ExecuteReaderWithLogging(this SqlCommand cmd, ILogger logger, bool logCommand = false)
        {
            try
            {
                if (logCommand)
                {
                    logger.LogDebug($"Executing query={cmd.CommandText}");
                }
                return cmd.ExecuteReader();
            }
            catch (Exception e)
            {
                logger.LogError($"Exception executing query. Message={e.Message}\nQuery={cmd.CommandText}");
                throw;
            }
        }
    }
}
