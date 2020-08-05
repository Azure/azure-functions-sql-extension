// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    public class SqlChangeTrackingEntry<T>
    {
        public WatcherChangeTypes ChangeType { get; set; }

        public T Data { get; set; }
    }
}
