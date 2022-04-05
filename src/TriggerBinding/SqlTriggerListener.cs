// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <typeparam name="T">A user-defined POCO that represents a row of the user's table</typeparam>
    internal sealed class SqlTriggerListener<T> : IListener
    {

        private readonly SqlTableChangeMonitor<T> _changeMonitor;
        private State _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerListener{T}" />>
        /// </summary>
        /// <param name="connectionString">
        /// The SQL connection string used to connect to the user's database
        /// </param>
        /// <param name="table"> 
        /// The name of the user table that changes are being tracked on
        /// </param>
        /// <param name="workerId">
        /// The worker application ID
        /// </param>
        /// <param name="executor">
        /// Used to execute the user's function when changes are detected on "table"
        /// </param>
        /// <param name="logger">
        /// 
        /// </param>
        public SqlTriggerListener(string table, string connectionString, string workerId, ITriggeredFunctionExecutor executor, ILogger logger)
        {
            this._changeMonitor = new SqlTableChangeMonitor<T>(table, connectionString, workerId, executor, logger);
            this._state = State.NotInitialized;
        }

        /// <summary>
        /// Stops the listener which stops checking for changes on the user's table
        /// </summary>
        public void Cancel()
        {
            this.StopAsync(CancellationToken.None).Wait();
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
            if (this._state == State.NotInitialized)
            {
                await this._changeMonitor.StartAsync();
                this._state = State.Running;
            }
        }

        /// <summary>
        /// Stops the listener (if it was started), which stops checking for changes on the user's table
        /// </summary>
        /// <param name="cancellationToken">Unused</param>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Nothing to stop if the change monitor has either already been stopped or hasn't been started
            if (this._state == State.Running)
            {
                this._changeMonitor.Stop();
                this._state = State.Stopped;
            }
            return Task.CompletedTask;
        }

        private enum State
        {
            Running,
            NotInitialized,
            Stopped
        }
    }
}