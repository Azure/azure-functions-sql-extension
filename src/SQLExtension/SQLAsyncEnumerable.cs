using Microsoft.Azure.WebJobs.Extensions.SQL;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SQLBindingExtension
{
    public class SQLAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        private readonly SqlConnectionWrapper _connection;
        private readonly SQLBindingAttribute _attribute;

        public SQLAsyncEnumerable(SqlConnectionWrapper connection, SQLBindingAttribute attribute)
        {
            if (connection == null || attribute == null)
            {
                throw new ArgumentNullException("Both the SqlConnection and SQLBindingAttribute must be non-null");
            }
            _connection = connection;
            _attribute = attribute;
        }
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new SQLAsyncEnumerator<T>(_connection, _attribute);
        }

        internal class SQLAsyncEnumerator<T> : IAsyncEnumerator<T>
        {
            private readonly SqlConnectionWrapper _connection;
            private readonly SQLBindingAttribute _attribute;
            private T _currentRow;
            private SqlDataReader _reader;

            public SQLAsyncEnumerator(SqlConnectionWrapper connection, SQLBindingAttribute attribute)
            {
                if (connection == null || attribute == null)
                {
                    throw new ArgumentNullException("Both the SqlConnection and SQLBindingAttribute must be non-null");
                }
                _connection = connection;
                _attribute = attribute;
            }

            public T Current => _currentRow == null ? throw new InvalidOperationException("Invalid attempt to get current element when no data is present") 
                : _currentRow;

            public ValueTask DisposeAsync()
            {
                _reader.Close();
                var connection = _connection.GetConnection();
                connection.Close();
                return new ValueTask(Task.CompletedTask);
            }

            public ValueTask<bool> MoveNextAsync()
            {
                return new ValueTask<bool>(GetNextRow());
            }

            private bool GetNextRow()
            {
                if (_reader == null)
                {
                    var connection = _connection.GetConnection();
                    try
                    {
                        SqlCommand command = new SqlCommand(_attribute.SQLQuery, connection);
                        command.Connection.Open();
                        _reader = command.ExecuteReader();
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException("Exception in executing query: " + e.Message);
                    }
                }
                if (_reader.Read())
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
