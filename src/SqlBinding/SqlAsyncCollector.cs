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
using System.Collections.Concurrent;
using System.IO;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlAsyncCollector<T> : IAsyncCollector<T>
    {
        private readonly IConfiguration _configuration;
        private readonly SqlAttribute _attribute;
        private readonly List<T> _rows;
        // Maps from database name + table name to SqlCommandBuilders
        private static ConcurrentDictionary<string, SqlCommandBuilder> _commandBuilders = new ConcurrentDictionary<string, SqlCommandBuilder>();
        // Maps from database name + table name to a byte array which contains information about that table's schema
        private static ConcurrentDictionary<string, byte[]> _schemas = new ConcurrentDictionary<string, byte[]>();

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
        /// <param name="cancellationToken">The cancellationToken is not used in this method</param>
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
        /// <param name="cancellationToken">The cancellationToken is not used in this method</param>
        /// <returns> A CompletedTask if executed successfully. If no rows were added, this is returned 
        /// automatically. </returns>
        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            if (_rows.Count != 0)
            {
                string rows = JsonConvert.SerializeObject(_rows);
                await UpsertRowsAsync(rows, _attribute, _configuration);
                _rows.Clear();
            }
        }


        /// <summary>
        /// Upserts the rows specified in "rows" to the table specified in "attribute"
        /// If a primary key in "rows" already exists in the table, the row is interpreted as an update rather than an insert.
        /// The column values associated with that primary key in the table are updated to have the values specified in "rows".
        /// If a new primary key is encountered in "rows", the row is simply inserted into the table. 
        /// </summary>
        /// <param name="rows"> The rows to be upserted </param>
        /// <param name="attribute"> Contains the name of the table to be modified and SQL connection information </param>
        /// <param name="configuration"> Used to build up the connection </param>
        private async Task UpsertRowsAsync(string rows, SqlAttribute attribute, IConfiguration configuration)
        {

            using (var connection = SqlBindingUtilities.BuildConnection(attribute, configuration))
            {
                var tableName = attribute.CommandText;
                // In the case that the user specified the table name as something like 'dbo.Products', we split this into
                // 'dbo' and 'Products' to build the select query in the SqlDataAdapter. In that case, the length of the
                // tableNameComponents array is 2. Otherwise, the user specified a table name without the prefix so we 
                // just surround it by brackets
                var tableNameComponents = tableName.Split(new[] { '.' }, 2);
                if (tableNameComponents.Length == 2)
                {
                    tableName = $"[{tableNameComponents[0]}].[{tableNameComponents[1]}]";
                } else
                {
                    tableName = $"[{tableName}]";
                }

                DataSet dataSet = new DataSet();
                DataTable newData = (DataTable)JsonConvert.DeserializeObject(rows, typeof(DataTable));

                await connection.OpenAsync();
                SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
                SqlDataAdapter dataAdapter = new SqlDataAdapter(new SqlCommand($"SELECT * FROM {tableName};", connection, transaction));
                // Specifies which column should be intepreted as the primary key
                dataAdapter.FillSchema(newData, SchemaType.Source);
                newData.TableName = tableName;
                DataTable originalData = newData.Clone();
                // Get the rows currently stored in table
                dataAdapter.Fill(originalData);
                // Merge them with the new data. This will mark a row as "modified" if both originalData and newData have
                // the same primary key. If newData has new primary keys, those rows as marked as "inserted"
                originalData.Merge(newData);
                dataSet.Tables.Add(originalData);

                var key = connection.Database + tableName;
                SqlCommandBuilder builder;
                // First time we've encountered this table, meaning we should create the command builder that uses the SelectCommand 
                // of the dataAdapter to discover the table schema and generate other commands
                if (!_commandBuilders.TryGetValue(key, out builder))
                {
                    builder = new SqlCommandBuilder(dataAdapter);
                    _commandBuilders.TryAdd(key, builder);
                }
                else
                {
                    // Commands have already been generated, so we just need to attach them to the dataAdapter. No need to 
                    // discover the table schema
                    var insertCommand = builder.GetInsertCommand();
                    insertCommand.Connection = connection;
                    insertCommand.Transaction = transaction;
                    dataAdapter.InsertCommand = insertCommand;
                    var updateCommand = builder.GetUpdateCommand();
                    updateCommand.Connection = connection;
                    updateCommand.Transaction = transaction;
                    dataAdapter.UpdateCommand = updateCommand;
                }
                dataAdapter.UpdateBatchSize = 1000;
                dataAdapter.Update(dataSet, tableName);
                await transaction.CommitAsync();
            }
        }
    }
}
