using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.SQL;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace SQLBindingExtension
{
    internal class SQLCollectors
    {
        internal class SQLAsyncCollector<T> : IAsyncCollector<T>
        {
            private readonly SqlConnectionWrapper _connection;
            private readonly SQLBindingAttribute _attribute;
            private readonly List<T> _rows;

            public SQLAsyncCollector(SqlConnectionWrapper connection, SQLBindingAttribute attribute)
            {
                _connection = connection;
                _attribute = attribute;
                _rows = new List<T>();
            }

            public Task AddAsync(T item, CancellationToken cancellationToken = default)
            {
                _rows.Add(item);
                return Task.CompletedTask;
            }

            public Task FlushAsync(CancellationToken cancellationToken = default)
            {
                if (_rows.Count == 0)
                {
                    return Task.CompletedTask;
                }

                string rows = JsonConvert.SerializeObject(_rows);
                InsertRows(rows, _attribute.SQLQuery, _connection.GetConnection());
                _rows.Clear();
                return Task.CompletedTask;
            }
        }

        internal class SQLCollector<T> : ICollector<T>
        {
            private readonly SqlConnectionWrapper _connection;
            private readonly SQLBindingAttribute _attribute;

            public SQLCollector(SqlConnectionWrapper connection, SQLBindingAttribute attribute)
            {
                _connection = connection;
                _attribute = attribute;
            }

            public void Add(T item)
            {
                string row = "[" + JsonConvert.SerializeObject(item) + "]";
                InsertRows(row, _attribute.SQLQuery, _connection.GetConnection());
            }
        }
        private static void InsertRows(string rows, string table, SqlConnection connection)
        {
            DataTable dataTable = (DataTable)JsonConvert.DeserializeObject(rows, typeof(DataTable));
            dataTable.TableName = table;
            DataSet dataSet = new DataSet();
            dataSet.Tables.Add(dataTable);
            var dataAdapter = new SqlDataAdapter("SELECT * FROM " + table + ";", connection);
            SqlCommandBuilder commandBuilder = new SqlCommandBuilder(dataAdapter);
            connection.Open();
            dataAdapter.Update(dataSet, table);
            connection.Close();
        }
    }
}
