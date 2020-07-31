// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Description;
using System;
using System.Data;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// An input and output binding that can be used to either:
    /// - Establish a connection to a SQL server database and extract the results of a query run against that database, in the case of an input binding
    /// - Establish a connection to a SQL server database and insert rows into a given table, in the case of an output binding
    /// </summary>
    [Binding]
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class SqlTriggerAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlAttribute/>"/> class.
        /// </summary>
        /// <param name="commandText">The text of the command</param>
        public SqlTriggerAttribute(string commandText)
        {
            CommandText = commandText ?? throw new ArgumentNullException(nameof(commandText));
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
        /// For an input binding, either a SQL query or stored procedure that will be run in the database referred to in the ConnectionString.
        /// For an output binding, the table name.
        /// </summary>
        public string CommandText { get; set; }
    }
}
