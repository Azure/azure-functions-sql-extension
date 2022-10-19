// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;

namespace Microsoft.Azure.Functions.Worker.Extension.Sql
{
    public class SqlOutputAttribute : OutputBindingAttribute
    {
        /// <summary>
        /// Creates an instance of the <see cref="SqlAttribute"/>, specifying the Sql attributes
        /// the function supports.
        /// </summary>
        /// <param name="commandText">The text of the command.</param>
        public SqlOutputAttribute(string commandText)
        {
            this.CommandText = commandText;
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
        public string ConnectionStringSetting { get; set; }

        /// <summary>
        /// The table name to upsert the values to.
        /// </summary>
        public string CommandText { get; set; }

        /// <summary>
        /// Specifies <see cref="CommandText"/> as Text.
        /// </summary>
        public System.Data.CommandType CommandType { get; } = System.Data.CommandType.Text;
    }
}
