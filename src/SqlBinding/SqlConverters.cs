// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlConverters
    {
        internal class SqlConverter : IConverter<SqlAttribute, SqlCommand>
        {
            private IConfiguration _configuration;

            /// <summary>
            /// Initializes a new instance of the <see cref="SqlConverter/>"/> class.
            /// </summary>
            /// <param name="configuration"></param>
            /// <exception cref="ArgumentNullException">
            /// Thrown if the configuration is null
            /// </exception>
            public SqlConverter(IConfiguration configuration)
            {
                _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            }

            /// <summary>
            /// Creates a SqlCommand containing a SQL connection and the SQL query and parameters specified in attribute.
            /// The user can open the connection in the SqlCommand and use it to read in the results of the query themselves. 
            /// </summary>
            /// <param name="attribute">
            /// Contains the SQL query and parameters as well as the information necessary to build the SQL Connection
            /// </param>
            /// <returns>The SqlCommand</returns>
            public SqlCommand Convert(SqlAttribute attribute)
            {
                var connection = SqlBindingUtilities.BuildConnection(attribute, _configuration);
                SqlCommand command = new SqlCommand(attribute.CommandText, connection);
                SqlBindingUtilities.ParseParameters(attribute.Parameters, command);
                return command;
            }

        }

        internal class SqlGenericsConverter<T> : IConverter<SqlAttribute, IEnumerable<T>>, IConverter<SqlAttribute, IAsyncEnumerable<T>>,
            IConverter<SqlAttribute, string>
        {
            private IConfiguration _configuration;

            /// <summary>
            /// Initializes a new instance of the <see cref="SqlGenericsConverter<typeparamref name="T"/>"/> class.
            /// </summary>
            /// <param name="configuration"></param>
            /// <exception cref="ArgumentNullException">
            /// Thrown if the configuration is null
            /// </exception>
            public SqlGenericsConverter(IConfiguration configuration) 
            {
                _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            }

            /// <summary>
            /// Opens a SqlConnection, reads in the data from the user's database, and returns it as a list of POCOs.
            /// </summary>
            /// <param name="attribute">
            /// Contains the information necessary to establish a SqlConnection, and the query to be executed on the database
            /// </param>
            /// <returns>An IEnumerable containing the rows read from the user's database in the form of the user-defined POCO</returns>
            public IEnumerable<T> Convert(SqlAttribute attribute)
            {
                string json = Task.Run<string>(() => BuildItemFromAttribute(attribute)).GetAwaiter().GetResult();
                return JsonConvert.DeserializeObject<IEnumerable<T>>(json);
            }

            /// <summary>
            /// Opens a SqlConnection, reads in the data from the user's database, and returns it as a JSON-formatted string.
            /// </summary>
            /// <param name="attribute">
            /// Contains the information necessary to establish a SqlConnection, and the query to be executed on the database
            /// </param>
            /// <returns>
            /// The JSON string. I.e., if the result has two rows from a table with schema ProductID: int, Name: varchar, Cost: int, 
            /// then the returned JSON string could look like
            /// [{"productID":3,"name":"Bottle","cost":90},{"productID":5,"name":"Cup","cost":100}]
            /// </returns>
            string IConverter<SqlAttribute, string>.Convert(SqlAttribute attribute)
            {
                //https://docs.microsoft.com/en-us/archive/msdn-magazine/2015/july/async-programming-brownfield-async-development#the-thread-pool-hack
                //Does the BuildItemFromAttribute method use "per-thread" state?
                return Task.Run<string>(() => BuildItemFromAttribute(attribute)).GetAwaiter().GetResult();
            }

            /// <summary>
            /// Extracts the <see cref="SqlAttribute.ConnectionStringSetting"/> in attribute and uses it to establish a connection
            /// to the SQL database. (Must be virtual for mocking the method in unit tests)
            /// </summary>
            /// <param name="attribute">
            /// The binding attribute that contains the name of the connection string app setting and query.
            /// </param>
            /// <returns></returns>
            public virtual async Task<string> BuildItemFromAttribute(SqlAttribute attribute)
            {
                using (var connection = SqlBindingUtilities.BuildConnection(attribute, _configuration))
                {
                    using (SqlDataAdapter adapter = new SqlDataAdapter())
                    {
                        SqlCommand command = SqlBindingUtilities.BuildCommand(attribute, connection);
                        adapter.SelectCommand = command;
                        await connection.OpenAsync();
                        DataTable dataTable = new DataTable();
                        adapter.Fill(dataTable);
                        return JsonConvert.SerializeObject(dataTable);
                    }
                }
            }

            IAsyncEnumerable<T> IConverter<SqlAttribute, IAsyncEnumerable<T>>.Convert(SqlAttribute attribute)
            {
                return new SqlAsyncEnumerable<T>(SqlBindingUtilities.BuildConnection(attribute, _configuration), attribute);
            }
        }
    }
}
