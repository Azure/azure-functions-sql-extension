// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;

namespace Microsoft.Azure.Functions.Worker.Extension.Sql
{
    public sealed class SqlInputAttribute : InputBindingAttribute
    {
        /// <summary>
        /// Creates an instance of the <see cref="SqlAttribute"/>, specifying the Sql attributes
        /// the function supports.
        /// </summary>
        /// <param name="commandText">The text of the command.</param>
        public SqlInputAttribute(string commandText)
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
        /// Either a SQL query or stored procedure that will be run in the database referred to in the ConnectionString.
        /// </summary>
        public string CommandText { get; set; }

        /// <summary>
        /// Specifies whether <see cref="CommandText"/> refers to a stored procedure or SQL query string.
        /// Use <see cref="CommandType.StoredProcedure"/> for the former, <see cref="CommandType.Text"/> for the latter
        /// </summary>
        public System.Data.CommandType CommandType { get; set; } = System.Data.CommandType.Text;

        /// <summary>
        /// Specifies the parameters that will be used to execute the SQL query or stored procedure specified in <see cref="CommandText"/>.
        /// Must follow the format "@param1=param1,@param2=param2". For example, if your SQL query looks like
        /// "select * from Products where cost = @Cost and name = @Name", then Parameters must have the form "@Cost=100,@Name={Name}"
        /// If the value of a parameter should be null, use "null", as in @param1=null,@param2=param2".
        /// If the value of a parameter should be an empty string, do not add anything after the equals sign and before the comma,
        /// as in "@param1=,@param2=param2"
        /// Note that neither the parameter name nor the parameter value can have ',' or '='
        /// </summary>
        public string Parameters { get; set; }
    }
}
