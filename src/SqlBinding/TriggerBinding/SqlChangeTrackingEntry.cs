using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    public class SqlChangeTrackingEntry<T>
    {
        public WatcherChangeTypes ChangeType { get; set; }

        public T Data { get; set; }
    }
}
