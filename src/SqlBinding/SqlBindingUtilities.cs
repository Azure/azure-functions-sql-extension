// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal static class SqlBindingUtilities
    {
        /// <summary>
        /// Builds a connection using the connection string attached to the app setting with name ConnectionStringSetting
        /// </summary>
        /// <param name="attribute">The name of the app setting that stores the SQL connection string</param>
        /// <param name="configuration">Used to obtain the value of the app setting</param>
        /// <exception cref="ArgumentException">
        /// Thrown if ConnectionStringSetting is empty or null
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if configuration is null
        /// </exception>
        /// <returns>The built connection </returns>
        public static SqlConnection BuildConnection(string connectionStringSetting, IConfiguration configuration)
        {
            return new SqlConnection(GetConnectionString(connectionStringSetting, configuration));
        }

        public static string GetConnectionString(string connectionStringSetting, IConfiguration configuration)
        {
            if (string.IsNullOrEmpty(connectionStringSetting))
            {
                throw new ArgumentException("Must specify ConnectionStringSetting, which should refer to the name of an app setting that " +
                    "contains a SQL connection string");
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            return configuration.GetConnectionStringOrSetting(connectionStringSetting);
        }

        /// <summary>
        /// Parses the parameter string into a list of parameters, where each parameter is separted by "," and has the form 
        /// "@param1=param2". "@param1" is the parameter name to be used in the query or stored procedure, and param1 is the 
        /// parameter value. Parameter name and parameter value are separated by "=". Parameter names/values cannot contain ',' or '='. 
        /// A valid parameter string would be "@param1=param1,@param2=param2". Attaches each parsed parameter to command.
        /// If the value of a parameter should be null, use "null", as in @param1=null,@param2=param2". 
        /// If the value of a parameter should be an empty string, do not add anything after the equals sign and before the comma,
        /// as in "@param1=,@param2=param2"
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
                throw new ArgumentNullException(nameof(command));
            }

            // If parameters is null, user did not specify any parameters in their function so nothing to parse
            if (!string.IsNullOrEmpty(parameters))
            {
                // Because we remove empty entries, we will ignore any commas that appear at the beginning/end of the parameter list,
                // as well as extra commas that appear between parameter pairs.
                // I.e., ",,@param1=param1,,@param2=param2,,," will be parsed just like "@param1=param1,@param2=param2" is.
                string[] paramPairs = parameters.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var pair in paramPairs)
                {
                    // Note that we don't throw away empty entries here, so a parameter pair that looks like "=@param1=param1"
                    // or "@param2=param2=" is considered malformed
                    string[] items = pair.Split('=');
                    if (items.Length != 2)
                    {
                        throw new ArgumentException("Parameters must be separated by \",\" and parameter name and parameter value must be separated by \"=\", " +
                           "i.e. \"@param1=param1,@param2=param2\". To specify a null value, use null, as in \"@param1=null,@param2=param2\"." +
                           "To specify an empty string as a value, simply do not add anything after the equals sign, as in \"@param1=,@param2=param2\".");
                    }
                    if (!items[0].StartsWith("@"))
                    {
                        throw new ArgumentException("Parameter name must start with \"@\", i.e. \"@param1=param1,@param2=param2\"");
                    }
                    

                    if (items[1].Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        command.Parameters.Add(new SqlParameter(items[0], DBNull.Value));
                    } 
                    else
                    {
                        command.Parameters.Add(new SqlParameter(items[0], items[1]));
                    }

                }
            }
        }

        /// <summary>
        /// Builds a SqlCommand using the query/stored procedure and parameters specifed in attribute.
        /// </summary>
        /// <param name="attribute">The SqlAttribute with the parameter, command type, and command text</param>
        /// <param name="connection">The connection to attach to the SqlCommand</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the CommandType specified in attribute is neither StoredProcedure nor Text. We only support
        /// commands that refer to the name of a StoredProcedure (the StoredProcedure CommandType) or are themselves 
        /// raw queries (the Text CommandType).
        /// </exception>
        /// <returns>The built SqlCommand</returns>
        public static SqlCommand BuildCommand(SqlAttribute attribute, SqlConnection connection)
        {
            SqlCommand command = new SqlCommand();
            command.Connection = connection;
            command.CommandText = attribute.CommandText;
            if (attribute.CommandType == CommandType.StoredProcedure)
            {
                command.CommandType = CommandType.StoredProcedure;
            }
            else if (attribute.CommandType != CommandType.Text)
            {
                throw new ArgumentException("The Type of the SQL attribute for an input binding must be either CommandType.Text for a plain text" +
                    "SQL query, or CommandType.StoredProcedure for a stored procedure.");
            }
            SqlBindingUtilities.ParseParameters(attribute.Parameters, command);
            return command;
        }

        /// <summary>
        /// Returns a dictionary where each key is a column name and each value is the SQL row's value for that column
        /// </summary>
        /// <param name="reader">
        /// Used to determine the columns of the table as well as the next SQL row to process
        /// </param>
        /// <param name="cols">
        /// Filled with the columns of the table if empty, otherwise assumed to be populated 
        /// with their names already (used for cacheing so we don't retrieve the column names every time)
        /// </param>
        /// <returns>The built dictionary</returns>
        public static Dictionary<string, string> BuildDictionaryFromSqlRow(SqlDataReader reader, List<string> cols)
        {
            if (cols.Count == 0)
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    cols.Add(reader.GetName(i));
                }
            }

            var result = new Dictionary<string, string>();
            foreach (var col in cols)
            {
                result.Add(col, reader[col].ToString());
            }
            return result;
        }

        /// <summary>
        /// Returns [tableName] if tableName is not prefixed by a schema, otherwise returns [schema].[table] in the case that
        /// tableName = schema.table
        /// </summary>
        /// <param name="tableName">The name of the user's table</param>
        /// <returns>The normalized table name</returns>
        public static string NormalizeTableName(string tableName)
        {
            // In the case that the user specified the table name as something like 'dbo.Products', we split this into
            // 'dbo' and 'Products' to build the select query in the SqlDataAdapter. In that case, the length of the
            // tableNameComponents array is 2. Otherwise, the user specified a table name without the prefix so we 
            // just surround it by brackets
            string[] tableNameComponents = tableName.Split(new[] { '.' }, 2);
            var schema = string.Empty;
            var table = string.Empty;
            if (tableNameComponents.Length == 2)
            {
                schema = tableNameComponents[0];
                table = tableNameComponents[1];
                // User didn't already surround the schema name with brackets
                if (!schema.StartsWith('[') && !schema.EndsWith(']'))
                {
                    schema = $"[{schema}]";
                }
            }
            else
            {
                table = tableName;
            }

            // User didn't already surround the table name with brackets
            if (!table.StartsWith('[') && !table.EndsWith(']'))
            {
                table = $"[{table}]";
            }

            if (!String.IsNullOrEmpty(schema))
            {
                return $"{schema}.{table}";
            }
            else
            {
                return table;
            }
        }

        public static void GetTableAndSchema(string fullName, out string schema, out string tableName)
        {
            // defaults
            tableName = fullName;
            schema = string.Empty;

            // remove [ ] from name if necessary
            string cleanName = fullName.Replace("]", string.Empty).Replace("[", string.Empty);

            // if in format schema.table, split into two parts for query
            string[] pieces = cleanName.Split('.');

            if (pieces.Length == 2)
            {
                schema = pieces[0];
                tableName = pieces[1];
            }
        }

        /// <summary>
        /// Attaches SqlParameters to "command". Each parameter follows the format (@PrimaryKey_i, PrimaryKeyValue), where @PrimaryKey is the
        /// name of a primary key column, and PrimaryKeyValue is one of the row's value for that column. To distinguish between the parameters
        /// of different rows, each row will have a distinct value of i.
        /// </summary>
        /// <param name="command">The command the parameters are attached to</param>
        /// <param name="rows">The rows to which this command corresponds to</param>
        /// <param name="primaryKeys">List of primary key column names</param>
        /// <remarks>
        /// Ideally, we would have a map that maps from rows to a list of SqlCommands populated with their primary key values. The issue with
        /// this is that SQL doesn't seem to allow adding parameters to one collection when they are part of another. So, for example, since
        /// the SqlParameters are part of the list in the map, an exception is thrown if they are also added to the collection of a SqlCommand.
        /// The expected behavior seems to be to rebuild the SqlParameters each time
        /// </remarks>
        public static void AddPrimaryKeyParametersToCommand(SqlCommand command, List<Dictionary<string, string>> rows, IEnumerable<string> primaryKeys)
        {
            var index = 0;
            foreach (var row in rows)
            {
                foreach (var key in primaryKeys)
                {
                    row.TryGetValue(key, out string primaryKeyValue);
                    command.Parameters.Add(new SqlParameter($"@{key}_{index}", primaryKeyValue));
                }
                index++;
            }
        }
    }
}