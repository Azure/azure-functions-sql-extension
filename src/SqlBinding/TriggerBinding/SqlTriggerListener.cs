// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <typeparam name="T">A user-defined POCO that represents a row of the user's table</typeparam>
    internal class SqlTriggerListener<T> : IListener
    {
        // Can't use an enum for these because it doesn't work with the Interlocked class
        private int _status;
        private const int ListenerNotRegistered = 0;
        private const int ListenerRegistering = 1;
        private const int ListenerRegistered = 2;

        private readonly SqlTableWatcher<T> _watcher;
        private readonly ILogger logger;

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
            _status = ListenerNotRegistered;
            _watcher = new SqlTableWatcher<T>(table, connectionString, executor, logger);
        }

        /// <summary>
        /// Stops the listener which stops checking for changes on the user's table
        /// </summary>
        public void Cancel()
        {
            StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Disposes resources held by the listener to poll for changes
        /// </summary>
        public void Dispose()
        {
            // Nothing to dispose
        }

        /// <summary>
        /// Starts the listener, which starts polling for changes on the user's table
        /// </summary>
        /// <param name="cancellationToken">Unused</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if StartAsync is called more than once
        /// </exception>
        /// <returns></returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            int previousStatus = Interlocked.CompareExchange(ref _status, ListenerRegistering, ListenerNotRegistered);

            if (previousStatus == ListenerRegistering)
            {
                throw new InvalidOperationException("The listener is already starting.");
            }
            else if (previousStatus == ListenerRegistered)
            {
                throw new InvalidOperationException("The listener has already started.");
            }
            try
            {
                await _watcher.StartAsync();
                Interlocked.CompareExchange(ref _status, ListenerRegistered, ListenerRegistering);
            }
            catch (Exception ex)
            {
                _status = ListenerNotRegistered;
                throw ex;
            }

        }

        /// <summary>
        /// Stops the listener which stops checking for changes on the user's table
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _watcher.StopAsync();
            _status = ListenerNotRegistered;
        }
    }
}