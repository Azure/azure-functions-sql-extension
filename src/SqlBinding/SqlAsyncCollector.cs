// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlAsyncCollector<T> : IAsyncCollector<T>
    {
        private readonly IConfiguration _configuration;
        private readonly SqlAttribute _attribute;
        private readonly List<T> _rows;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlAsyncCollector<typeparamref name="T"/>"/> class.
        /// </summary>
        /// <param name="connection"> 
        /// Contains the SQL connection that will be used by the collector when it inserts SQL rows 
        /// into the user's table 
        /// </param>
        /// <param name="attribute"> 
        /// Contains as one of its attributes the SQL table that rows will be inserted into 
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either configuration or attribute is null
        /// </exception>
        public SqlAsyncCollector(IConfiguration configuration, SqlAttribute attribute)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
            _rows = new List<T>();
        }

        /// <summary>
        /// Adds an item to this collector that is processed in a batch along with all other items added via 
        /// AddAsync when <see cref="FlushAsync"/> is called. Each item is interpreted as a row to be added to the SQL table
        /// specified in the SQL Binding.
        /// </summary>
        /// <param name="item"> The item to add to the collector </param>
        /// <param name="cancellationToken"></param>
        /// <returns> A CompletedTask if executed successfully </returns>
        public Task AddAsync(T item, CancellationToken cancellationToken = default)
        {
            if (item != null)
            {
                _rows.Add(item);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Processes all items added to the collector via <see cref="AddAsync"/>. Each item is interpreted as a row to be added
        /// to the SQL table specified in the SQL Binding. All rows are added in one transaction. Nothing is done
        /// if no items were added via AddAsync.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns> A CompletedTask if executed successfully. If no rows were added, this is returned 
        /// automatically. </returns>
        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            if (_rows.Count != 0)
            {
                string rows = JsonConvert.SerializeObject(_rows);
                await InsertRowsAsync(rows, _attribute, _configuration);
                _rows.Clear();
            }
        }


        /// <summary>
        /// Adds the rows specified in "rows" to "table", a SQL table in the user's database, using "connection"
        /// </summary>
        /// <param name="rows"> The rows to be inserted </param>
        /// <param name="attribute"> Contains the name of the table to be modified and SQL connection information </param>
        /// <param name="configuration"> Used to build up the 
        /// connection </param>
        private static async Task InsertRowsAsync(string rows, SqlAttribute attribute, IConfiguration configuration)
        {
            var table = attribute.CommandText;
            DataTable dataTable = (DataTable)JsonConvert.DeserializeObject(rows, typeof(DataTable));
            dataTable.TableName = table;
            DataSet dataSet = new DataSet();
            dataSet.Tables.Add(dataTable);
            using (var connection = SqlBindingUtilities.BuildConnection(attribute, configuration))
            {
                await connection.OpenAsync();
                var transaction = connection.BeginTransaction();
                // The command builder actually executes the select command attached to the data adapter to get information
                // about the table. If the select command returns a lot of rows this could be an unnecessarily heavy operation, 
                // so best to just grab one row
                var dataAdapter = new SqlDataAdapter(new SqlCommand($"SELECT TOP 1 * FROM [{table}];", connection, transaction));
                SqlCommandBuilder commandBuilder = new SqlCommandBuilder(dataAdapter);
                // Obviously shouldn't hardcode this value in. Is batching something we want to support? 
                dataAdapter.UpdateBatchSize = 1000;
                dataAdapter.Update(dataSet, table);
                transaction.Commit();
            }
        }
    }
}
