// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Represents the changed row in the user table.
    /// </summary>
    /// <typeparam name="T">POCO class representing the row in the user table</typeparam>
    public sealed class SqlChange<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlChange{T}"/> class.
        /// </summary>
        /// <param name="operation">Change operation</param>
        /// <param name="item">POCO representing the row in the user table on which the change operation took place</param>
        public SqlChange(SqlChangeOperation operation, T item)
        {
            this.Operation = operation;
            this.Item = item;
        }

        /// <summary>
        /// Change operation (insert, update, or delete).
        /// </summary>
        public SqlChangeOperation Operation { get; }

        /// <summary>
        /// POCO representing the row in the user table on which the change operation took place. If the change
        /// operation is <see cref="SqlChangeOperation.Delete">, then only the properties corresponding to the primary
        /// keys will be populated.
        /// </summary>
        public T Item { get; }
    }

    /// <summary>
    /// Represents the type of change operation in the table row.
    /// </summary>
    public enum SqlChangeOperation
    {
        Insert,
        Update,
        Delete
    }
}