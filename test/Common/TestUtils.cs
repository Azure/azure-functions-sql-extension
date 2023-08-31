// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    /// <remarks>
    /// Adapted from Microsoft.VisualStudio.TeamSystem.Data.UnitTests.UnitTestUtilities.TestDBManager
    /// </remarks>
    public static class TestUtils
    {
        internal static int ThreadId;

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

            using (IDbCommand cmd = connection.CreateCommand())
            {
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

            using (IDbCommand cmd = connection.CreateCommand())
            {
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
            };
        }

        public static string GetPathToBin()
        {
            return Path.GetDirectoryName(Assembly.GetAssembly(typeof(Product)).Location);
        }
    }
}
