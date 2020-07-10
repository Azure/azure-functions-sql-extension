using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.SQL;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace SQLBindingExtension
{
    public class SQLCollectors
    {
        public class SQLAsyncCollector<T> : IAsyncCollector<T>
        {
            private readonly SqlConnectionWrapper _connection;
            private readonly SQLBindingAttribute _attribute;
            private readonly List<T> _rows;

            /// <summary>
            /// Builds a SQLAsynCollector
            /// </summary>
            /// <param name="connection"> 
            /// Contains the SQL connection that will be used by the collector when it inserts SQL rows 
            /// into the user's table 
            /// </param>
            /// <param name="attribute"> 
            /// Contains as one of its attributes the SQL table that rows will be inserted into 
            /// </param>
            /// <exception cref="ArgumentNullException">
            /// Thrown if either connection or attribute is null
            /// </exception>
            public SQLAsyncCollector(SqlConnectionWrapper connection, SQLBindingAttribute attribute)
            {
                if (connection == null || attribute == null)
                {
                    throw new ArgumentNullException("Both the SqlConnection and SQLBindingAttribute must be non-null");
                }
                _connection = connection;
                _attribute = attribute;
                _rows = new List<T>();
            }

            /// <summary>
            /// Adds an item to this collector that is processed in a batch along with all other items added via 
            /// AddAsync when FlushAsync is called. Each item is interpreted as a row to be added to the SQL table
            /// specified in the SQL Binding.
            /// </summary>
            /// <param name="item"> The item to add to the collector </param>
            /// <param name="cancellationToken"></param>
            /// <exception cref="ArgumentNullException">
            /// Thrown if the item is null
            /// </exception>
            /// <returns> A CompletedTask if executed successfully </returns>
            public Task AddAsync(T item, CancellationToken cancellationToken = default)
            {
                if (item == null)
                {
                    throw new ArgumentNullException("Item passed to AddAsync cannot be null");
                }
                _rows.Add(item);
                return Task.CompletedTask;
            }

            /// <summary>
            /// Processes all items added to the collector via AddAsync. Each item is interpreted as a row to be added
            /// to the SQL table specified in the SQL Binding. All rows are added in one transaction. Nothing is done
            /// if no items were added via AddAsync.
            /// </summary>
            /// <param name="cancellationToken"></param>
            /// <returns> A CompletedTask if executed successfully. If no rows were added, this is returned 
            /// automatically. </returns>
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

        public class SQLCollector<T> : ICollector<T>
        {
            private readonly SqlConnectionWrapper _connection;
            private readonly SQLBindingAttribute _attribute;

            /// <summary>
            /// Builds a SQLCollector
            /// </summary>
            /// <param name="connection"> 
            /// Contains the SQL connection that will be used by the collector when it inserts SQL rows 
            /// into the user's table 
            /// </param>
            /// <param name="attribute"> 
            /// Contains as one of its attributes the SQL table that rows will be inserted into 
            /// </param>
            /// <exception cref="ArgumentNullException">
            /// Thrown if either connection or attribute is null
            /// </exception>
            public SQLCollector(SqlConnectionWrapper connection, SQLBindingAttribute attribute)
            {
                if (connection == null || attribute == null)
                {
                    throw new ArgumentNullException("Both the SqlConnection and SQLBindingAttribute must be non-null");
                }
                _connection = connection;
                _attribute = attribute;
            }

            /// <summary>
            /// Adds an item to this collector that is processed immediately by this collector.
            /// Each item is interpreted as a row to be added to the SQL table specified in the SQL Binding.
            /// </summary>
            /// <param name="item"> The item to add to the collector </param>
            /// <exception cref="ArgumentNullException">
            /// Thrown if the item is null
            /// </exception>
            public void Add(T item)
            {
                if (item == null)
                {
                    throw new ArgumentNullException("Item passed to AddAsync cannot be null");
                }
                string row = "[" + JsonConvert.SerializeObject(item) + "]";
                InsertRows(row, _attribute.SQLQuery, _connection.GetConnection());
            }
        }

        /// <summary>
        /// Adds the rows specified in "rows" to "table", a SQL table in the user's database, using "connection"
        /// </summary>
        /// <param name="rows"> The rows to be inserted </param>
        /// <param name="table"> The name of the table that is being modified </param>
        /// <param name="connection"> The SqlConnection that has all connection and authentication information 
        /// already specified</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if an exception is encountered while executing the SQL transaction to insert the rows
        /// </exception>
        private static void InsertRows(string rows, string table, SqlConnection connection)
        {
            DataTable dataTable = (DataTable)JsonConvert.DeserializeObject(rows, typeof(DataTable));
            dataTable.TableName = table;
            DataSet dataSet = new DataSet();
            dataSet.Tables.Add(dataTable);
            try
            {
                var dataAdapter = new SqlDataAdapter("SELECT * FROM " + table + ";", connection);
                SqlCommandBuilder commandBuilder = new SqlCommandBuilder(dataAdapter);
                connection.Open();
                using (var bulk = new SqlBulkCopy(connection))
                {
                    bulk.DestinationTableName = table;
                    bulk.WriteToServer(dataTable);
                }
                connection.Close();
            } catch (Exception e)
            {
                throw new InvalidOperationException("Exception encountered when attempting to execute" +
                    "the SQL transaction: " + e.Message);
            }
        }
    }
}
