// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlTriggerListener : IListener
    {
        // Can't use an enum for these because it doesn't work with the Interlocked class
        private int _status;
        private const int ListenerNotRegistered = 0;
        private const int ListenerRegistering = 1;
        private const int ListenerRegistered = 2;

        private SqlTableWatcher _watcher;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerListener"/> class.
        /// </summary>
        /// <param name="connectionStringSetting"> 
        /// The name of the app setting that stores the SQL connection string
        /// </param>
        /// <param name="table"> 
        /// The name of the user table that changes are being tracked on
        /// </param>
        /// <param name="configuration">
        /// Used to extract the connection string from connectionStringSetting
        /// </param>
        /// <param name="executor">
        /// Used to execute the user's function when changes are detected on "table"
        /// </param>
        public SqlTriggerListener(string table, string connectionStringSetting, IConfiguration configuration, ITriggeredFunctionExecutor executor)
        {
            _status = ListenerNotRegistered;
            _watcher = new SqlTableWatcher(table, connectionStringSetting, configuration, executor);
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
