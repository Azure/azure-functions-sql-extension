using Microsoft.Azure.WebJobs.Description;
using System;

namespace Microsoft.Azure.WebJobs.Extensions.SQL
{
    /// <summary>
    /// An input binding that can be used to establish a connection to a SQL server database and extract the results of a query run against that database.
    /// </summary>
    [Binding]
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    public sealed class SQLBindingAttribute : Attribute
    {
        /// <summary>
        /// The connection string used when creating a SQL connection (see https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection?view=dotnet-plat-ext-3.1).
        /// The attributes specific in the connection string are listed here https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection.connectionstring?view=dotnet-plat-ext-3.1
        /// For example, to create a connection to the "TestDB" located at the URL "test.database.windows.net", the ConnectionString would look like 
        /// "Data Source=test.database.windows.net;Database=TestDB". 
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The SQL query that will be run in the database referred to in the ConnectionString.
        /// </summary>
        [AutoResolve]
        public string SQLQuery { get; set; }

        /// <summary>
        /// An optional parameter that specifies authentication information necessary to access the server. The Authentication string should refer to the app setting
        /// in your Function app that contains the authentication information, i.e. "%SQLServerAuthentication%" is the app setting is named SQLServer Authentication.
        /// The value of the app setting needs to follow the specific format "User ID=<userid>;Password=<password>". The user ID and password are extracted and
        /// passed to the SQL connection as a Credential (see https://docs.microsoft.com/en-us/dotnet/api/system.data.sqlclient.sqlconnection.credential?view=dotnet-plat-ext-3.1#System_Data_SqlClient_SqlConnection_Credential)
        /// </summary>
        [AutoResolve]
        public string Authentication { get; set; }
    }
}
