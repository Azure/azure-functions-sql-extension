// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Represents a row that was changed in the user's table as well as metadata related to that change
    /// </summary>
    /// <remarks>
    /// Note that there is a chance the <see cref="Data"/> field does not reflect the most recent version of the row in the user's table.
    /// There is also a chance that the data does not accurately reflect <see cref="ChangeType"/>. For example,
    /// say that a row was updated and then deleted, and this SqlChangeTrackingEntry corresponds to the first change, the update.
    /// In that case, ChangeType will be <see cref="SqlChangeType.Updated"/>, but by the time the user table is queried
    /// for the row, the row has been deleted, so Data will only be populated with the primary key values of the deleted row.
    /// </remarks>
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
        /// The current data in the user's table corresponding to the change. 
        /// Note that there is a chance changeType and data are not synchronized (see "remarks" section in the class comment)
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