// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;

namespace Microsoft.Azure.Functions.Worker.Extensions.Sql
{
    public class SqlOutputAttribute : OutputBindingAttribute
    {
        /// <summary>
        /// Creates an instance of the <see cref="SqlOutputAttribute"/>, which takes a list of rows and upserts them into the target table.
        /// </summary>
        /// <param name="commandText">The table name to upsert the values to.</param>
        /// <param name="connectionStringSetting">The name of the app setting where the SQL connection string is stored</param>
        public SqlOutputAttribute(string commandText, string connectionStringSetting)
        {
            this.CommandText = commandText ?? throw new ArgumentNullException(nameof(commandText));
            this.ConnectionStringSetting = connectionStringSetting ?? throw new ArgumentNullException(nameof(connectionStringSetting));
        }

        /// <summary>
        /// The name of the app setting where the SQL connection string is stored
        /// (see https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection).
        /// The attributes specified in the connection string are listed here
        /// https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring
        /// For example, to create a connection to the "TestDB" located at the URL "test.database.windows.net" using a User ID and password,
        /// create a ConnectionStringSetting with a name like SqlServerAuthentication. The value of the SqlServerAuthentication app setting
        /// would look like "Data Source=test.database.windows.net;Database=TestDB;User ID={userid};Password={password}".
        /// </summary>
        public string ConnectionStringSetting { get; }

        /// <summary>
        /// The table name to upsert the values to.
        /// </summary>
        public string CommandText { get; }
    }
}
