// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <typeparam name="T">A user-defined POCO that represents a row of the user's table</typeparam>
    internal class SqlTriggerListener<T> : IListener
    {

        private readonly SqlTableWatchers.SqlTableChangeMonitor<T> _watcher;
        private State _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerListener<typeparamref name="T"/>>
        /// </summary>
        /// <param name="connectionString">
        /// The SQL connection string used to connect to the user's database
        /// </param>
        /// <param name="table"> 
        /// The name of the user table that changes are being tracked on
        /// </param>
        /// <param name="executor">
        /// Used to execute the user's function when changes are detected on "table"
        /// </param>
        public SqlTriggerListener(string table, string connectionString, ITriggeredFunctionExecutor executor, ILogger logger)
        {
            _watcher = new SqlTableWatchers.SqlTableChangeMonitor<T>(table, connectionString, executor, logger);
            _state = State.NotRegistered;
        }

        /// <summary>
        /// Stops the listener which stops checking for changes on the user's table
        /// </summary>
        public void Cancel()
        {
            StopAsync(CancellationToken.None).Wait();
        }

        /// <summary>
        /// Disposes resources held by the listener to poll for changes
        /// </summary>
        public void Dispose()
        {
            // Nothing to dispose
        }

        /// <summary>
        /// Starts the listener if it has not yet been started, which starts polling for changes on the user's table
        /// </summary>
        /// <param name="cancellationToken">Unused</param>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_state == State.NotRegistered)
            {
                await _watcher.StartAsync();
                _state = State.Registered;
            }
        }

        /// <summary>
        /// Stops the listener (if it was started), which stops checking for changes on the user's table
        /// </summary>
        /// <param name="cancellationToken">Unused</param>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Nothing to stop if the watcher has either already been stopped or hasn't been started
            if (_state == State.Registered)
            {
                _watcher.Stop();
                _state = State.Stopped;
            }
            return Task.CompletedTask;
        }

        enum State
        {
            Registered,
            NotRegistered,
            Stopped
        }
    }
}