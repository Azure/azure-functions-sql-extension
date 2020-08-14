// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Represents a row that was changed in the user's table as well as metadata related to that change.
    /// If the row was deleted, then <see cref="Data"/> is populated only with the primary key values of the deleted row
    /// <typeparam name="T">A user-defined POCO that represents a row of the table</typeparam>
    public class SqlChangeTrackingEntry<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlChangeTrackingEntry<typeparamref name="T"/>"/> class.
        /// </summary>
        /// <param name="changeType">
        /// The type of change this entry corresponds to
        /// </param>
        /// <param name="data">
        /// The current data in the user's table corresponding to the change (and only the primary key values 
        /// of the row in the case that it was deleted)
        /// </param>
        public SqlChangeTrackingEntry(SqlChangeType changeType, T data)
        {
            ChangeType = changeType;
            Data = data;
        }

        /// <summary>
        /// Specifies the type of change that occurred to the row. 
        /// <see cref="SqlChangeType.Updated"/> corresponds to an update, 
        /// <see cref="SqlChangeType.Inserted"/> corresponds to an insert, 
        /// <see cref="SqlChangeType.Deleted"/> corresponds to a delete
        /// </summary>
        public SqlChangeType ChangeType { get; }

        /// <summary>
        /// A copy of the row that was updated/inserted in the user's table.
        /// In the case that the row no longer exists in the user's table, Data is only populated with the primary key values
        /// of the deleted row.
        /// </summary>
        public T Data { get; }
    }

    public enum SqlChangeType
    {
        Inserted,
        Updated,
        Deleted
    }
}