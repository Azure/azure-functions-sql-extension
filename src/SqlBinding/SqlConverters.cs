using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    public class SqlConverters
    {
        public class SqlConverter : IConverter<SqlAttribute, SqlCommand>
        {
            private SqlConnectionWrapper _connection;
            private IConfiguration _configuration;

            /// <summary>
            /// Used for testing, so we can pass a dummy SqlConnection to the converter and avoid it trying to build 
            /// an actual one
            /// </summary>
            /// <param name="connection"> The dummy SqlConnection </param>
            public SqlConverter(SqlConnectionWrapper connection)
            {
                _connection = connection;
            }

            public SqlConverter(IConfiguration configuration)
            {
                _configuration = configuration;
            }

            public SqlCommand Convert(SqlAttribute attribute)
            {
                _connection = BuildConnection(_connection, attribute, _configuration);
                SqlCommand command = new SqlCommand(attribute.Command, _connection.GetConnection());
                return command;
            }

        }

        public class SqlGenericsConverter<T> : IConverter<SqlAttribute, IEnumerable<T>>, IConverter<SqlAttribute, IAsyncEnumerable<T>>,
            IConverter<SqlAttribute, string>
        {
            private SqlConnectionWrapper _connection;
            private IConfiguration _configuration;

            /// <summary>
            /// Used for testing, so we can pass a dummy SqlConnection to the converter and avoid it trying to build 
            /// an actual one
            /// </summary>
            /// <param name="connection"> The dummy SqlConnection </param>
            public SqlGenericsConverter(SqlConnectionWrapper connection)
            {
                _connection = connection;
            }

            public SqlGenericsConverter(IConfiguration configuration) 
            {
                _configuration = configuration;
            }

            /// <summary>
            /// Opens a SqlConnection, reads in the data from the user's database, and returns it as a list of POCOs. Throws an exception if 
            /// the SqlConnection cannot be established or the data cannot be read from the database <see cref="BuildItemFromAttribute(SqlAttribute)"/>
            /// </summary>
            /// <param name="attribute">
            /// Contains the information necessary to establish a SqlConnection, and the query to be executed on the database
            /// </param>
            /// <returns>An IEnumerable containing the rows read from the user's database in the form of the user-defined POCO</returns>
            public IEnumerable<T> Convert(SqlAttribute attribute)
            {
                string json = BuildItemFromAttribute(attribute);
                return JsonConvert.DeserializeObject<IEnumerable<T>>(json);
            }

            string IConverter<SqlAttribute, string>.Convert(SqlAttribute attribute)
            {
                return BuildItemFromAttribute(attribute);
            }

            /// <summary>
            /// Extracts the ConnectionStringSetting in attribute and uses it (in combination with the Authentication string, if provided) to establish a connection
            /// to the SQL database. (Must be virtual for mocking the method in unit tests)
            /// </summary>
            /// <param name="attribute">
            /// The binding attribute that contains the connection string, authentication, and query.
            /// </param>
            /// <exception cref="InvalidOperationException">
            /// Thrown if an exception occurs when opening the SQL connection or when running the query.
            /// </exception>
            /// <returns></returns>
            public virtual string BuildItemFromAttribute(SqlAttribute attribute)
            {
                _connection = BuildConnection(_connection, attribute, _configuration);
                using (SqlConnection connection = _connection.GetConnection())
                {
                    using (SqlDataAdapter adapter = new SqlDataAdapter())
                    {
                        try
                        {
                            SqlCommand command = BuildCommand(attribute, connection);
                            adapter.SelectCommand = command;
                            connection.Open();
                            DataTable dataTable = new DataTable();
                            adapter.Fill(dataTable);
                            return JsonConvert.SerializeObject(dataTable);
                        }

                        catch (Exception e)
                        {
                            throw new InvalidOperationException("Exception in executing query: " + e.Message);
                        }
                    }
                }
            }

            IAsyncEnumerable<T> IConverter<SqlAttribute, IAsyncEnumerable<T>>.Convert(SqlAttribute attribute)
            {
                return new SqlAsyncEnumerable<T>(BuildConnection(null, attribute, _configuration), attribute);
            }
        }

        /// <summary>
        /// Public for testing.
        /// Builds a connection using the connection string and authentication information specified in "attribute". 
        /// Only builds a new connection if "connection" is null. Otherwise just returns "connection" 
        /// </summary>
        /// <param name="connection">Used to determine whether or not a new connection must be built. The function 
        /// simply returns "connection" if it is non-null </param>
        /// <param name="attribute">Contains the connection string and authentication information for the user's database</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the connection string in "attribute" is null
        /// </exception>
        /// <returns>The built connection </returns>
        public static SqlConnectionWrapper BuildConnection(SqlConnectionWrapper connection, SqlAttribute attribute, IConfiguration configuration)
        {
            // A non-null connections indicates that we already have a SqlConnectionWrapper from the unit tests
            if (connection == null)
            {
                if (attribute.ConnectionStringSetting == null)
                {
                    throw new InvalidOperationException("Must specify a ConnectionStringSetting, which refers to the name of an app setting which contains" +
                        "the SQL connection string, to connect to your SQL server instance.");
                }
                if (configuration == null)
                {
                    throw new ArgumentNullException("configuration");
                }
                connection = new SqlConnectionWrapper(configuration.GetConnectionStringOrSetting(attribute.ConnectionStringSetting));
            }
            return connection;
        }

        /// <summary>
        /// Public for testing.
        /// Builds a SqlCommand using the query/stored procedure and parameters specifed in attribute.
        /// </summary>
        /// <param name="attribute">The SqlAttribute with the parameter, command type, and command text</param>
        /// <param name="connection">The connection to attach to the SqlCommand</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the CommandType specified in attribute is neither StoredProcedure nor Text. We only support
        /// commands that refer to the name of a StoredProcedure (the StoredProcedure CommandType) or are themselves 
        /// raw queries (the Text CommandType).
        /// </exception>
        /// <returns>The build SqlCommand</returns>
        public static SqlCommand BuildCommand(SqlAttribute attribute, SqlConnection connection)
        {
            SqlCommand command = new SqlCommand();
            command.Connection = connection;
            command.CommandText = attribute.Command;
            if (attribute.Type == CommandType.StoredProcedure)
            {
                command.CommandType = CommandType.StoredProcedure;
            }
            else if (attribute.Type != CommandType.Text)
            {
                throw new ArgumentException("The Type of the Sql attribute for an input binding must be either CommandType.Text for a plain text" +
                    "Sql query, or CommandType.StoredProcedure for a stored procedure.");
            }
            ParseParameters(attribute.Parameters, command);
            return command;
        }

        /// <summary>
        /// Public for testing.
        /// Parses the parameter string into a list of parameters, where each parameter is separted by "," and has the form 
        /// "@param1=param2". "@param1" is the parameter name to be used in the query or stored procedure, and param1 is the 
        /// parameter value. Parameter name and parameter value are separated by "=". Parameter names/values cannot contain ',' or '='. 
        /// A valid parameter string would be "@param1=param1,@param2=param2". Attaches each parsed parameter to command.
        /// </summary>
        /// <param name="parameters">The parameter string to be parsed</param>
        /// <param name="command">The SqlCommand to which the parsed parameters will be added to</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if command is null
        /// </exception>
        public static void ParseParameters(string parameters, SqlCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }

            // If parameters is null, user did not specify any parameters in their function so nothing to parse
            if (parameters != null)
            {
                // Because we remove empty entries, we will ignore any commas that appear at the beginning/end of the parameter list.
                // I.e., ",,@param1=param1,@param2=param2,,," will be parsed just like "@param1=param1,@param2=param2" is.
                // Do we want this? 
                string[] credentials = parameters.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var pair in credentials)
                {
                    // Note that we don't throw away empty entries here, so a parameter pair that looks like "=@param1=param1"
                    // or "@param2=param2=" is considered malformed
                    string[] items = pair.Split('=');
                    if (items.Length != 2)
                    {
                        throw new ArgumentException("Parameters must be separated by \",\" and parameter name and parameter value must be separated by \"=\", " +
                            "i.e. \"@param1=param1,@param2=param2\"");
                    }
                    if (!items[0].StartsWith("@"))
                    {
                        throw new ArgumentException("Parameter name must start with \"@\", i.e. \"@param1=param1,@param2=param2\"");
                    }
                    command.Parameters.Add(new SqlParameter(items[0], items[1]));
                }
            }
        }
    }
}
