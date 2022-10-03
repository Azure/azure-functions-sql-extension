// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;

namespace Microsoft.Azure.Functions.Worker.Sql
{
    public class SqlOutputAttribute : OutputBindingAttribute
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="commandText">The text of the command.</param>
        /// <param name="connectionStringSetting">The name of the app setting where the SQL connection string is stored.</param>
        public SqlOutputAttribute(string commandText, string connectionStringSetting)
        {
            this.CommandText = commandText;
            this.ConnectionStringSetting = connectionStringSetting;
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
        /// For an output binding, the table name.
        /// </summary>
        public string CommandText { get; }

        /// <summary>
        /// Specifies whether <see cref="CommandText"/> refers to a stored procedure or SQL query string.
        /// Use <see cref="CommandType.StoredProcedure"/> for the former, <see cref="CommandType.Text"/> for the latter
        /// </summary>
        public CommandType CommandType { get; set; } = CommandType.Text;
    }
}
