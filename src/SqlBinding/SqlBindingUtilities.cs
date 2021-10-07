// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.SqlServer.TransactSql.ScriptDom;

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
        /// Parses the parameter string into a list of parameters, where each parameter is separated by "," and has the form
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

                foreach (string pair in paramPairs)
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
                    if (!items[0].StartsWith("@", StringComparison.InvariantCultureIgnoreCase))
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
        /// Builds a SqlCommand using the query/stored procedure and parameters specified in attribute.
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
            var command = new SqlCommand
            {
                Connection = connection,
                CommandText = attribute.CommandText
            };
            if (attribute.CommandType == CommandType.StoredProcedure)
            {
                command.CommandType = CommandType.StoredProcedure;
            }
            else if (attribute.CommandType != CommandType.Text)
            {
                throw new ArgumentException("The Type of the SQL attribute for an input binding must be either CommandType.Text for a plain text" +
                    "SQL query, or CommandType.StoredProcedure for a stored procedure.");
            }
            ParseParameters(attribute.Parameters, command);
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
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    cols.Add(reader.GetName(i));
                }
            }

            var result = new Dictionary<string, string>();
            foreach (string col in cols)
            {
                result.Add(col, reader[col].ToString());
            }
            return result;
        }

        /// <summary>
        /// Escape any existing quotes and add quotes around the identifier.
        /// </summary>
        /// <param name="identifier">The string to quote.</param>
        /// <returns>The escaped and quoted string.</returns>
        public static string QuoteIdentifier(string identifier)
        {
            return $"'{identifier.Replace("'", "''")}'";
        }

        /// <summary>
        /// Use ScriptDom to parse schema and tableName and return them with quotes.
        /// If there is no schema in fullName, SCHEMA_NAME() is returned as schema.
        /// </summary>
        /// <param name="fullName">
        /// Full name of table, including schema (if exists).
        /// </param>
        public static void GetTableAndSchema(string fullName, out string quotedSchema, out string quotedTableName)
        {
            var parser = new TSql150Parser(false);
            var stringReader = new StringReader(fullName);
            SchemaObjectName tree = parser.ParseSchemaObjectName(stringReader, out IList<ParseError> errors);

            if (errors.Count > 0)
            {
                string errorMessages = "Encountered error(s) while parsing schema and table name:\n";
                foreach (ParseError err in errors)
                {
                    errorMessages += $"{err.Message}\n";
                }
                throw new InvalidOperationException(errorMessages);
            }

            var visitor = new QuotedTSqlFragmentVisitor();
            tree.Accept(visitor);
            quotedSchema = visitor.quotedSchema;
            quotedTableName = visitor.quotedTableName;
        }

        /// <summary>
        /// Get the schema and table name with quotes from the SchemaObjectName.  
        /// </summary>
        private class QuotedTSqlFragmentVisitor : TSqlFragmentVisitor
        {
            internal string quotedSchema;
            internal string quotedTableName;

            public override void Visit(SchemaObjectName node)
            {
                this.quotedSchema = node.SchemaIdentifier != null ? QuoteIdentifier(node.SchemaIdentifier.Value) : "SCHEMA_NAME()";
                this.quotedTableName = QuoteIdentifier(node.BaseIdentifier.Value);
            }
        }
    }
}