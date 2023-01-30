// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Description;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to bind a parameter to SQL trigger message.
    /// </summary>
    [Binding]
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class SqlTriggerAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerAttribute"/> class, which triggers the function when any changes on the specified table are detected.
        /// </summary>
        /// <param name="tableName">Name of the table to watch for changes.</param>
        public SqlTriggerAttribute(string tableName)
        {
            this.TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        }

        /// <summary>
        /// Name of the app setting containing the SQL connection string.
        /// </summary>
        [ConnectionString]
        public string ConnectionStringSetting { get; set; }

        /// <summary>
        /// Name of the table to watch for changes.
        /// </summary>
        public string TableName { get; }
    }
}