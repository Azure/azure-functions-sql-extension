using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Security;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Config;

namespace Microsoft.Azure.WebJobs.Extensions.SQL
{
    /// <summary>
    /// Attempts to establish a connection to the SQL server and database specified in the ConnectionString and run the query 
    /// specified in SQLQuery <see cref="SQLBindingAttribute"/>
    /// </summary>
    [Extension("SQLBinding")]
    internal class SQLBinding : IExtensionConfigProvider
    {
        public void Initialize(ExtensionConfigContext context)
        {
            var rule = context.AddBindingRule<SQLBindingAttribute>();
            rule.BindToInput<string>(BuildItemFromAttribute);
        }

        /// <summary>
        /// Extracts the ConnectionString in arg and uses it (in combination with the Authentication string, if provided) to establish a connection
        /// to the SQL database. 
        /// </summary>
        /// <param name="arg">
        /// The binding attribute that contains the connection string, authentication, and query.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the ConnectionString in arg is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if an exception occurs when opening the SQL connection or when running the query.
        /// </exception>
        /// <returns></returns>
        private string BuildItemFromAttribute(SQLBindingAttribute arg)
        {
            if (arg.ConnectionString == null)
            {
                throw new ArgumentNullException("Must specify a connection string to connect to your SQL server instance.");
            }

            string result = string.Empty;
            SqlConnection connection = new SqlConnection(arg.ConnectionString);
            connection.Credential = GetCredential(arg.Authentication);

            using (connection)
            {
                try
                {
                    SqlCommand command = new SqlCommand(arg.SQLQuery, connection);
                    command.Connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result += String.Format("ID: {0}, Product Name: {1}, Price: {2}", reader[0], reader[1], reader[2]) + "\n";
                        }
                    }
                } 
                catch (Exception e)
                {
                    throw new InvalidOperationException("Exception in executing query: " + e.Message);
                }

            }

            return result;
        }

        /// <summary>
        /// Extracts the User ID and password from the authentication string if authentication string is not null and returns a SqlCredential object
        /// with the authentication information. Returns null if authentication is null.
        /// </summary>
        /// <param name="authentication">
        /// The authentication string, must follow the format "User ID=<userid>;Password=<password>"
        /// </param>
        /// <returns>
        /// SqlCredential object with User ID and password in the authentication string. Null if authentication string is null.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the authentication string is malformed.
        /// </exception>
        private SqlCredential GetCredential(string authentication)
        {
            if (authentication == null)
            {
                return null;
            }

            string[] credentials = authentication.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            Dictionary<string, string> dict = new Dictionary<string, string>();
            foreach (var pair in credentials)
            {
                string[] items = pair.Split('=');
                dict.Add(items[0], items[1]);
            }

            string passwordStr;
            string username;
            if (!dict.TryGetValue("User ID", out username))
            {
                throw new ArgumentException("User ID must be specified in the Authentication string as \"User ID =<userid>;\"");
            }

            if (!dict.TryGetValue("Password", out passwordStr))
            {
                throw new ArgumentException("Password must be specified in the Authentication string as \"Password =<password>;\"");
            }

            SecureString password = new SecureString();
            for (int i = 0; i < passwordStr.Length; i++)
            {
                password.AppendChar(passwordStr[i]);
            }
            password.MakeReadOnly();
            return new SqlCredential(username, password);
        }
    }
}