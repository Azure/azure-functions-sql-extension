// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    public class SqlAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        private readonly SqlConnectionWrapper _connection;
        private readonly SqlAttribute _attribute;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlAsyncEnumerable<typeparamref name="T"/>"/> class.
        /// </summary>
        /// <param name="connection">The SqlConnection to be used by the enumerator</param>
        /// <param name="attribute">The attribute containing the query, parameters, and query type</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either connection or attribute is null
        /// </exception>
        public SqlAsyncEnumerable(SqlConnectionWrapper connection, SqlAttribute attribute)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
        }
        /// <summary>
        /// Returns the enumerator associated with this enumerable. The enumerator will execute the query specified
        /// in attribute and "lazily" grab the Sql rows corresponding to the query result. It will only read a 
        /// row into memory if <see cref="MoveNextAsync"/> is called
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>The enumerator</returns>
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new SqlAsyncEnumerator<T>(_connection, _attribute);
        }


        internal class SqlAsyncEnumerator<T> : IAsyncEnumerator<T>
        {
            private readonly SqlConnectionWrapper _connection;
            private readonly SqlAttribute _attribute;
            private T _currentRow;
            private SqlDataReader _reader;

            /// <summary>
            /// Initializes a new instance of the <see cref="SqlAsyncEnumerator<typeparamref name="T"/>"/> class.
            /// </summary>
            /// <param name="connection">The SqlConnection to be used by the enumerator</param>
            /// <param name="attribute">The attribute containing the query, parameters, and query type</param>
            /// <exception cref="ArgumentNullException">
            /// Thrown if either connection or attribute is null
            /// </exception>
            public SqlAsyncEnumerator(SqlConnectionWrapper connection, SqlAttribute attribute)
            {
                _connection = connection ?? throw new ArgumentNullException(nameof(connection));
                _attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
            }

            /// <summary>
            /// Returns the current row of the query result that the enumerator is on
            /// </summary>
            /// <exception cref="InvalidOperationException">
            /// Thrown if Current is called before a call to <see cref="MoveNextAsync"/> is ever made, or if Current is called
            /// after <see cref="MoveNextAsync"/> has moved through all of the rows returned by the query.
            /// </exception>
            public T Current => _currentRow == null ? throw new InvalidOperationException("Invalid attempt to get current element when no data is present") 
                : _currentRow;

            /// <summary>
            /// Closes the Sql connection and resources associated with reading the results of the query
            /// </summary>
            /// <returns></returns>
            public ValueTask DisposeAsync()
            {
                // Doesn't seem like there's an async version of closing the reader/connection 
                _reader.Close();
                var connection = _connection.GetConnection();
                connection.Close();
                return new ValueTask(Task.CompletedTask);
            }

            /// <summary>
            /// Moves the enumerator to the next row of the Sql query result
            /// </summary>
            /// <returns> 
            /// True if there is another row left in the query to process, or false if this was the last row
            /// </returns>
            public ValueTask<bool> MoveNextAsync()
            {
                return new ValueTask<bool>(GetNextRow());
            }

            /// <summary>
            /// Attempts to grab the next row of the Sql query result. 
            /// </summary>
            /// <returns>
            /// True if there is another row left in the query to process, or false if this was the last row
            /// </returns>
            private async Task<bool> GetNextRow()
            {
                if (_reader == null)
                {
                    var connection = _connection.GetConnection();
                    SqlCommand command = SqlConverters.BuildCommand(_attribute, connection);
                    await command.Connection.OpenAsync();
                    _reader = await command.ExecuteReaderAsync();
                }
                if (await _reader.ReadAsync())
                {
                    _currentRow = JsonConvert.DeserializeObject<T>(SerializeRow());
                    return true;
                }
                else
                {
                    _currentRow = default(T);
                    return false;
                }
            }

            private string SerializeRow()
            {
                var cols = new List<string>();
                for (var i = 0; i < _reader.FieldCount; i++)
                {
                    cols.Add(_reader.GetName(i));
                }
                var result = new Dictionary<string, object>();
                foreach (var col in cols)
                {
                    result.Add(col, _reader[col]);
                }
                return JsonConvert.SerializeObject(result);
            }
        }
    }
}
