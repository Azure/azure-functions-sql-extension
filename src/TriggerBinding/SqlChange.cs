// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Represents a row that was changed in the user's table as well as metadata related to that change.
    /// If the row was deleted, then <see cref="Item"/> is populated only with the primary key values of the deleted row
    /// </summary>
    /// <typeparam name="T">A user-defined POCO that represents a row of the table</typeparam>
    public sealed class SqlChange<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlChange{T}"/> class.
        /// </summary>
        /// <param name="operation">
        /// The type of change this item corresponds to.
        /// </param>
        /// <param name="item">
        /// The current item in the user's table corresponding to the change (and only the primary key values of the row
        /// in the case that it was deleted).
        /// </param>

        public SqlChange(SqlChangeOperation operation, T item)
        {
            this.Operation = operation;
            this.Item = item;
        }

        /// <summary>
        /// Specifies the type of change that occurred to the row.
        /// </summary>
        public SqlChangeOperation Operation { get; }

        /// <summary>
        /// A copy of the row that was updated/inserted in the user's table.
        /// In the case that the row no longer exists in the user's table, Data is only populated with the primary key values
        /// of the deleted row.
        /// </summary>
        public T Item { get; }
    }

    public enum SqlChangeOperation
    {
        Insert,
        Update,
        Delete
    }
}