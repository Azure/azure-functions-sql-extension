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
        private const int ListenerNotRegistered = 0;
        private const int ListenerRegistering = 1;
        private const int ListenerRegistered = 2;

        private SqlTableWatcher _watcher;
        private int _status;

        public SqlTriggerListener(string table, string connectionStringSetting, IConfiguration configuration, ITriggeredFunctionExecutor executor)
        {
            _status = ListenerNotRegistered;
            _watcher = new SqlTableWatcher(table, connectionStringSetting, configuration, executor);
        }

        public void Cancel()
        {
            this.StopAsync(CancellationToken.None).Wait();
        }

        public void Dispose()
        {
            // Do I need to dispose for the SqlTableWatcher somehow?
            throw new NotImplementedException();
        }

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

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _watcher.StopAsync();
            _status = ListenerNotRegistered;
        }
    }
}
