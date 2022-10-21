// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlBindingConstants;
namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <typeparam name="T">A user-defined POCO that represents a row of the user's table</typeparam>
    internal class SqlAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        public SqlConnection Connection { get; private set; }
        private readonly SqlAttribute _attribute;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlAsyncEnumerable<typeparamref name="T"/>"/> class.
        /// </summary>
        /// <param name="connection">The SqlConnection to be used by the enumerator</param>
        /// <param name="attribute">The attribute containing the query, parameters, and query type</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either connection or attribute is null
        /// </exception>
        public SqlAsyncEnumerable(SqlConnection connection, SqlAttribute attribute)
        {
            this.Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            this._attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
            this.Connection.Open();
        }
        /// <summary>
        /// Returns the enumerator associated with this enumerable. The enumerator will execute the query specified
        /// in attribute and "lazily" grab the SQL rows corresponding to the query result. It will only read a
        /// row into memory if <see cref="MoveNextAsync"/> is called
        /// </summary>
        /// <param name="cancellationToken">The cancellationToken is not used in this method</param>
        /// <returns>The enumerator</returns>
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new SqlAsyncEnumerator(this.Connection, this._attribute);
        }


        private class SqlAsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly SqlConnection _connection;
            private readonly SqlAttribute _attribute;
            private SqlDataReader _reader;
            /// <summary>
            /// Initializes a new instance of the <see cref="SqlAsyncEnumerator<typeparamref name="T"/>"/> class.
            /// </summary>
            /// <param name="connection">The SqlConnection to be used by the enumerator</param>
            /// <param name="attribute">The attribute containing the query, parameters, and query type</param>
            /// <exception cref="ArgumentNullException">
            /// Thrown if either connection or attribute is null
            /// </exception>
            public SqlAsyncEnumerator(SqlConnection connection, SqlAttribute attribute)
            {
                this._connection = connection ?? throw new ArgumentNullException(nameof(connection));
                this._attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
            }

            /// <summary>
            /// Returns the current row of the query result that the enumerator is on. If Current is called before a call
            /// to <see cref="MoveNextAsync"/> is ever made, it will return null. If Current is called after
            /// <see cref="MoveNextAsync"/> has moved through all of the rows returned by the query, it will return
            /// the last row of the query.
            /// </summary>
            public T Current { get; private set; }

            /// <summary>
            /// Closes the SQL connection and resources associated with reading the results of the query
            /// </summary>
            /// <returns></returns>
            public ValueTask DisposeAsync()
            {
                // Doesn't seem like there's an async version of closing the reader/connection
                this._reader?.Close();
                this._connection.Close();
                return new ValueTask(Task.CompletedTask);
            }

            /// <summary>
            /// Moves the enumerator to the next row of the SQL query result
            /// </summary>
            /// <returns>
            /// True if there is another row left in the query to process, or false if this was the last row
            /// </returns>
            public ValueTask<bool> MoveNextAsync()
            {
                return new ValueTask<bool>(this.GetNextRowAsync());
            }

            /// <summary>
            /// Attempts to grab the next row of the SQL query result.
            /// </summary>
            /// <returns>
            /// True if there is another row left in the query to process, or false if this was the last row
            /// </returns>
            private async Task<bool> GetNextRowAsync()
            {
                // check connection state before trying to access the reader
                // if DisposeAsync has already closed it due to the issue described here https://github.com/Azure/azure-functions-sql-extension/issues/350
                if (this._connection.State != System.Data.ConnectionState.Closed)
                {
                    if (this._reader == null)
                    {
                        using (SqlCommand command = SqlBindingUtilities.BuildCommand(this._attribute, this._connection))
                        {
                            this._reader = await command.ExecuteReaderAsync();
                        }
                    }
                    if (await this._reader.ReadAsync())
                    {
                        this.Current = JsonConvert.DeserializeObject<T>(this.SerializeRow());
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// Serializes the reader's current SQL row into JSON
            /// </summary>
            /// <returns>JSON string version of the SQL row</returns>
            private string SerializeRow()
            {
                var jsonSerializerSettings = new JsonSerializerSettings()
                {
                    DateFormatString = ISO_8061_DATETIME_FORMAT
                };
                return JsonConvert.SerializeObject(SqlBindingUtilities.BuildDictionaryFromSqlRow(this._reader), jsonSerializerSettings);
            }
        }
    }
}