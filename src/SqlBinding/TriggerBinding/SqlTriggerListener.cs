using Microsoft.Azure.WebJobs.Host.Listeners;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlTriggerListener : IListener
    {
        SqlTableWatcher _watcher;

        public void Cancel()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _watcher = new SqlTableWatcher();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
