// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;

namespace Microsoft.Azure.Functions.Worker.Extensions.Sql
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SqlTriggerAttribute"/> class, which triggers the function when any changes on the specified table are detected.
    /// </summary>
    /// <param name="tableName">Name of the table to watch for changes.</param>
    /// <param name="connectionStringSetting">The name of the app setting where the SQL connection string is stored</param>
    /// <param name="leasesTableName">Optional - The name of the table used to store leases. If not specified, the leases table name will be Leases_{FunctionId}_{TableId}</param>
    public sealed class SqlTriggerAttribute(string tableName, string connectionStringSetting, string leasesTableName = null) : TriggerBindingAttribute
    {


        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerAttribute"/> class with null value for LeasesTableName.
        /// </summary>
        /// <param name="tableName">Name of the table to watch for changes.</param>
        /// <param name="connectionStringSetting">The name of the app setting where the SQL connection string is stored</param>
        public SqlTriggerAttribute(string tableName, string connectionStringSetting) : this(tableName, connectionStringSetting, null) { }

        /// <summary>
        /// Name of the app setting containing the SQL connection string.
        /// </summary>
        public string ConnectionStringSetting { get; } = connectionStringSetting ?? throw new ArgumentNullException(nameof(connectionStringSetting));

        /// <summary>
        /// Name of the table to watch for changes.
        /// </summary>
        public string TableName { get; } = tableName ?? throw new ArgumentNullException(nameof(tableName));

        /// <summary>
        /// Name of the table used to store leases.
        /// If not specified, the leases table name will be Leases_{FunctionId}_{TableId}
        /// More information on how this is generated can be found here
        /// https://github.com/Azure/azure-functions-sql-extension/blob/main/docs/TriggerBinding.md#az_funcleasestablename
        /// </summary>
        public string LeasesTableName { get; } = leasesTableName;
    }
}