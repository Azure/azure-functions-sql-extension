using Microsoft.Azure.WebJobs.Description;
using System;
using System.Data;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// An input binding that can be used to establish a connection to a SQL server database and extract the results of a query run against that database.
    /// </summary>
    [Binding]
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    public sealed class SqlAttribute : Attribute
    {
        public SqlAttribute(string command)
        {
            Command = command;
        }

        /// <summary>
        /// The connection string used when creating a SQL connection 
        /// (see https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection?view=dotnet-plat-ext-3.1).
        /// The attributes specificied in the connection string are listed here
        /// https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection.connectionstring?view=dotnet-plat-ext-3.1
        /// For example, to create a connection to the "TestDB" located at the URL "test.database.windows.net" using a User ID and password, 
        /// the ConnectionStringSetting would look like "Data Source=test.database.windows.net;Database=TestDB;User ID={userid};Password={password}". 
        /// </summary>
        public string ConnectionStringSetting { get; set; }

        /// <summary>
        /// For an input binding, either a SQL query or stored procedure that will be run in the database referred to in the ConnectionString.
        /// For an output binding, the table name.
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Specifies whether "Command" refers to a stored procedure or Sql query string. Use CommandType.StoredProcedure for the former,
        /// CommandType.Text for the latter
        /// </summary>
        public CommandType Type { get; set; }

        /// <summary>
        /// Specifies the parameters that will be used to execute the Sql query or stored procedure specified in "Command". Must follow the format
        /// "@param1=param1,@param2=param2". For example, if your Sql query looks like "select * from Products where cost = @Cost and name = @Name", 
        /// then Parameters must have the form "@Cost=100,@Name=Computer"
        /// </summary>
        [AutoResolve]
        public string Parameters { get; set; }
    }
}
