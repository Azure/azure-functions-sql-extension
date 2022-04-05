// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Description;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// A trigger binding that can be used to establish a connection to a SQL server database and trigger a user's function
    /// whenever changes happen to a given table in that database
    /// </summary>
    [Binding]
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class SqlTriggerAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerAttribute/>"/> class.
        /// </summary>
        /// <param name="tableName">The name of the table to monitor for changes</param>
        public SqlTriggerAttribute(string tableName)
        {
            this.TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        }

        /// <summary>
        /// The name of the app setting where the SQL connection string is stored
        /// (see https://docs.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnection?view=sqlclient-dotnet-core-2.0).
        /// The attributes specified in the connection string are listed here
        /// https://docs.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring?view=sqlclient-dotnet-core-2.0
        /// For example, to create a connection to the "TestDB" located at the URL "test.database.windows.net" using a User ID and password,
        /// create a ConnectionStringSetting with a name like SqlServerAuthentication. The value of the SqlServerAuthentication app setting 
        /// would look like "Data Source=test.database.windows.net;Database=TestDB;User ID={userid};Password={password}". 
        /// </summary>
        public string ConnectionStringSetting { get; set; }

        /// <summary>
        /// The name of the table to monitor for changes
        /// </summary>
        public string TableName { get; }
    }
}