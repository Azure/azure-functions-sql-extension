// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Represents a row that was changed in the user's table as well as metadata related to that change
    /// </summary>
    /// <typeparam name="T">A user-defined POCO that represents a row of the table</typeparam>
    public class SqlChangeTrackingEntry<T>
    {
        /// <summary>
        /// Specifies the type of change that occurred to the row. 
        /// <see cref="WatcherChangeTypes.Changed"/> corresponds to an update, 
        /// <see cref="WatcherChangeTypes.Created"/> corresponds to an insert, 
        /// <see cref="WatcherChangeTypes.Deleted"/> corresponds to a delete
        /// </summary>
        public WatcherChangeTypes ChangeType { get; set; }

        /// <summary>
        /// A copy of the row that was updated/inserted in the user's table. In the case of 
        /// a delete, this is populated with a default(T) value. 
        /// Note that there is a chance this data does not reflect the most recent version of the row in the user's table
        /// There is also a chance that the data does not accurately reflect <see cref="ChangeType"/>. For example,
        /// say that a row was updated and then deleted, and this SqlChangeTrackingEntry corresponds to the first change, the update.
        /// In that case, ChangeType will be <see cref="WatcherChangeTypes.Changed"/>, but by the time the user table is queried
        /// for the row, the row has been deleted, so Data will be default(T).
        /// </summary>
        public T Data { get; set; }
    }
}
