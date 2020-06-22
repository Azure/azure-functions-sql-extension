using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq.Expressions;
using System.Security;
using System.Xml.XPath;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Config;

namespace SQLBinding
{
    [Extension("SQLBinding")]
    public class SQLBinding : IExtensionConfigProvider
    {
        public void Initialize(ExtensionConfigContext context)
        {
            var rule = context.AddBindingRule<SQLBindingAttribute>();
            rule.BindToInput<string>(BuildItemFromAttribute);
        }

        private string BuildItemFromAttribute(SQLBindingAttribute arg)
        {
            if (arg.ConnectionString == null)
            {
                throw new Exception("Must specify a connection string to connect to your SQL server instance.");
            }

            string result = string.Empty;
            SqlConnection connection = new SqlConnection(arg.ConnectionString);
            if (arg.Authentication != null)
            {
                connection.Credential = GetCredential(arg.Authentication);
            }

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
                    throw new Exception("Exception in executing query: " + e.Message);
                }

            }

            return result;
        }

        private SqlCredential GetCredential(string authentication)
        {
            string[] credentials = authentication.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            Dictionary<string, string> dict = new Dictionary<string, string>();
            foreach (var pair in credentials)
            {
                string[] items = pair.Split('=');
                dict.Add(items[0], items[1]);
            }

            SecureString password = new SecureString();
            string passwordStr;
            dict.TryGetValue("Password", out passwordStr);
            for (int i = 0; i < passwordStr.Length; i++)
            {
                password.AppendChar(passwordStr[i]);
            }
            password.MakeReadOnly();
            string username;
            dict.TryGetValue("Username", out username);
            return new SqlCredential(username, password);
        }
    }
}