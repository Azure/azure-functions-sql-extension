// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using MoreLinq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <typeparam name="T">A user-defined POCO that represents a row of the user's table</typeparam>
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
                // string rows = JsonConvert.SerializeObject(_rows);
                // todo: take a lock on _rows
                await UpsertRowsAsync(_rows, _attribute, _configuration);
                _rows.Clear();
            }
        }

        // Maps from database name + table name to SqlCommandBuilders
        private static ConcurrentDictionary<string, SqlCommandBuilder> _commandBuilders = new ConcurrentDictionary<string, SqlCommandBuilder>();

        private async Task<bool> OldImplementation(IEnumerable<T> rows, SqlAttribute attribute, IConfiguration configuration)
        {
            Stopwatch st = new Stopwatch();

            int batchsize = 1000;

            using (SqlConnection connection = SqlBindingUtilities.BuildConnection(attribute.ConnectionStringSetting, configuration))
            {
                string tableName = SqlBindingUtilities.NormalizeTableName(attribute.CommandText);

                DataSet dataSet = new DataSet();

                string stringRows = JsonConvert.SerializeObject(_rows);
                DataTable newData = (DataTable)JsonConvert.DeserializeObject(stringRows, typeof(DataTable));

                await connection.OpenAsync();

                st.Start();
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead))
                {
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
                    // First time we've encountered this table, meaning we should create the command builder that uses the SelectCommand 
                    // of the dataAdapter to discover the table schema and generate other commands
                    if (!_commandBuilders.TryGetValue(key, out SqlCommandBuilder builder))
                    {
                        builder = new SqlCommandBuilder(dataAdapter);
                        _commandBuilders.TryAdd(key, builder);
                    }
                    else
                    {
                        // Commands have already been generated, so we just need to attach them to the dataAdapter. No need to 
                        // discover the table schema
                        SqlCommand insertCommand = builder.GetInsertCommand();
                        insertCommand.Connection = connection;
                        insertCommand.Transaction = transaction;
                        dataAdapter.InsertCommand = insertCommand;
                        SqlCommand updateCommand = builder.GetUpdateCommand();
                        updateCommand.Connection = connection;
                        updateCommand.Transaction = transaction;
                        dataAdapter.UpdateCommand = updateCommand;
                    }
                    dataAdapter.UpdateBatchSize = batchsize;
                    dataAdapter.Update(dataSet, tableName);
                    await transaction.CommitAsync();
                }
            }

            st.Stop();

            int batches = rows.Count() / batchsize;
            string line = $"Time for all rows: {st.ElapsedMilliseconds} ms. Row count: {rows.Count()}. Batch size: {batchsize}. Total batches={batches}";
            Console.WriteLine(line);

            using (System.IO.StreamWriter file =
                    new System.IO.StreamWriter(@$"C:\temp\old-{batchsize}-fullbatch-{batches}.txt", true))
            {
                file.WriteLine($"{st.ElapsedMilliseconds}, {st.ElapsedMilliseconds / batches}");
            }
            return true;
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
        private async Task UpsertRowsAsync(IEnumerable<T> rows, SqlAttribute attribute, IConfiguration configuration)
        {
            //if (await OldImplementation(rows, attribute, configuration))
            //{
            //    return;
            //}

            using (SqlConnection connection = SqlBindingUtilities.BuildConnection(attribute.ConnectionStringSetting, configuration))
            {
                Stopwatch st = new Stopwatch();
                st.Start();
                string fullDatabaseAndTableName = attribute.CommandText;
                ObjectCache cachedTables = MemoryCache.Default;
                TableInformation tableInfo = cachedTables[fullDatabaseAndTableName] as TableInformation;

                if (tableInfo == null)
                {
                    tableInfo = TableInformation.RetrieveTableInformation(connection, fullDatabaseAndTableName);

                    // If we were unable to look up the primary keys, for whatever reason, return early. 
                    // We'll try again next time. TODO: do we need to have a max # of retries / backoff plan?
                    if (tableInfo == null)
                    {
                        return;
                    }

                    CacheItemPolicy policy = new CacheItemPolicy
                    {
                        // Re-look up the primary key(s) after 10 minutes (they should not change very often!)
                        AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10)
                    };

                    cachedTables.Set(fullDatabaseAndTableName, tableInfo, policy);
                }

                st.Stop();
                Console.WriteLine($"Time to find table: {st.ElapsedMilliseconds} ms.");
                // todo: is this the right way to do batching (all at the end)? I don't know that we support periodic flushing 
                // (can't seem to find any other bindings doing so)
                int batchSize = 1000;

                Stopwatch queryGen = new Stopwatch();
                int totalBatches = rows.Count() / batchSize;
                List<long> times = new List<long>(totalBatches);

                st.Restart();
                await connection.OpenAsync();
                foreach (IEnumerable<T> batch in rows.Batch(batchSize))
                {
                    var t = GenerateDataQueryForMerge(tableInfo, batch);
                    var newDataQuery = t.Item1;
                    var payload = t.Item2;
                    queryGen.Start();
                    var cmd = new SqlCommand($"{newDataQuery} {tableInfo.MergeQuery};", connection);
                    var par = cmd.Parameters.Add("@j", SqlDbType.NVarChar, -1);
                    par.Value = payload;
                    await cmd.ExecuteNonQueryAsync();
                    times.Add(queryGen.ElapsedMilliseconds);
                    queryGen.Restart();
                }
                await connection.CloseAsync();

                st.Stop();
                string line = $"Time for all rows: {st.ElapsedMilliseconds} ms. Row count: {rows.Count()}. Batch size: {batchSize}";
                Console.WriteLine(line);

                using (System.IO.StreamWriter file = new System.IO.StreamWriter(@$"C:\temp\new-{batchSize}-ms.txt", true))
                {
                    foreach (long batchTime in times)
                    {
                        file.WriteLine(batchTime);
                    }
                }
            }
        }
        /// <summary>
        ///  Generates T-SQL for data to be upserted using Merge
        /// </summary>
        /// <param name="table">Information about the table we will be upserting into</param>
        /// <param name="rows">Rows to be upserted</param>
        /// <returns>T-SQL containing data for merge</returns>

        private static Tuple<string, string> GenerateDataQueryForMerge(TableInformation table, IEnumerable<T> rows)
        {
            IList<T> rowsToUpsert = new List<T>();
            HashSet<string> uniqueUpdatedPrimaryKeys = new HashSet<string>();

            // If there are duplicate primary keys, we'll need to pick the LAST (most recent) row per primary key.
            foreach (T row in rows.Reverse())
            {
                string combinedPrimaryKey = string.Empty;
                // Look up primary key of T. Because we're going in the same order of fields every time,
                // we can assume that if two rows with the same primary key are in the list, they will collide
                foreach (PropertyInfo primaryKey in table.PrimaryKeys)
                {
                    combinedPrimaryKey += primaryKey.GetValue(row).ToString();
                }

                // If we have already seen this unique primary key, skip this row
                if (uniqueUpdatedPrimaryKeys.Contains(combinedPrimaryKey))
                {
                    continue;
                }

                // This is the first time we've seen this particular PK. Add this row to the upsert query.
                uniqueUpdatedPrimaryKeys.Add(combinedPrimaryKey);
                rowsToUpsert.Add(row);
            }

            // todo: SURELY there is a better way to do this.........
            IEnumerable<MemberInfo> pocoFields = typeof(T).GetMembers().Where(f => f.MemberType == MemberTypes.Property || f.MemberType == MemberTypes.Field);


            string columnNames = string.Join(",", pocoFields.Select(f => f.Name));
            string rowData = JsonConvert.SerializeObject(rowsToUpsert);
            string newDataQuery = $"WITH cte AS ( SELECT * FROM OPENJSON(@j) WITH ({string.Join(",", table.ColumnDefinitions)}) )";

            return Tuple.Create<string, string>(newDataQuery, rowData);
        }

        public class TableInformation
        {
            public IEnumerable<MemberInfo> PrimaryKeys { get; }

            /// <summary>
            /// List of all of the columns to upsert
            /// </summary>
            public IEnumerable<string> ColumnNames { get; }

            /// <summary>
            /// List of all of the columns, along with their data type, to use to turn JSON into table
            /// </summary>
            public IEnumerable<string> ColumnDefinitions { get; }

            /// <summary>
            /// T-SQL merge statement generated from primary keys
            /// and column names for a specific table.
            /// </summary>
            public string MergeQuery { get; }

            public TableInformation(IEnumerable<MemberInfo> primaryKeys, IEnumerable<string> columns, IEnumerable<string> columnsDefinition, string mergeQuery)
            {
                this.PrimaryKeys = primaryKeys;
                this.ColumnNames = columns;
                this.MergeQuery = mergeQuery;
                this.ColumnDefinitions = columnsDefinition;
            }

            public static TableInformation RetrieveTableInformation(SqlConnection sqlConnection, string fullName)
            {
                SqlBindingUtilities.GetTableAndSchema(fullName, out string schema, out string tableName);

                var schemaOrDefault = string.IsNullOrEmpty(schema) ? "SCHEMA_NAME()" : "'" + schema + "'"; // Use default user schema if only object name has been provided

                IEnumerable<string> columnNames = typeof(T).GetMembers().Where(f => f.MemberType == MemberTypes.Property || f.MemberType == MemberTypes.Field).Select(f => f.Name);

                var primaryKeys = new List<string>();
                var columnsDefinition = new List<string>();

                // 1. Query SQL to get primary keys
                string primaryKeyQuery = $@"
                    select 
                        COLUMN_NAME
                    from
                        INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    inner join
                        INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE ccu on ccu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME AND ccu.TABLE_NAME = tc.TABLE_NAME
                    where
                        tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                    and
                        tc.TABLE_NAME = '{tableName}'
                    and
                        tc.TABLE_SCHEMA = {schemaOrDefault}
                    ";

                try
                {
                    sqlConnection.Open();
                    SqlCommand cmd = new SqlCommand(primaryKeyQuery, sqlConnection);
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            primaryKeys.Add(rdr["COLUMN_NAME"].ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Throw a custom error so that it's easier to decipher.
                    string message = $"Encountered exception while retrieving primary keys for table '{tableName}.' Cannot generate upsert command without them.";
                    throw new InvalidOperationException(message, ex);
                }
                finally
                {
                    sqlConnection.Close();
                }

                if (!primaryKeys.Any())
                {
                    string message = $"Did not retrieve any primary keys for '{tableName}.' Cannot generate upsert command without them.";
                    throw new InvalidOperationException(message);
                }

                string primaryKeyMatchingQuery = $"ExistingData.{primaryKeys[0]} = NewData.{primaryKeys[0]}";
                foreach (var primaryKey in primaryKeys.Skip(1))
                {
                    primaryKeyMatchingQuery += $" AND ExistingData.{primaryKey} = NewData.{primaryKey}";
                }

                // 2. Get Columns Definition
                string columnDefinitionQuery = $@"
                    select	
	                    COLUMN_NAME + ' ' + DATA_TYPE +
		                    case 
			                    when CHARACTER_MAXIMUM_LENGTH = -1 then '(max)'
			                    when CHARACTER_MAXIMUM_LENGTH <> -1 then '(' + cast(CHARACTER_MAXIMUM_LENGTH as varchar(4)) + ')'
			                    when DATETIME_PRECISION is not null then '(' + cast(DATETIME_PRECISION as varchar(1)) + ')'
			                    when DATETIME_PRECISION is not null then '(' + cast(DATETIME_PRECISION as varchar(1)) + ')'
			                    when DATA_TYPE in ('decimal', 'numeric') then '(' + cast(NUMERIC_PRECISION as varchar(9)) + ',' + + cast(NUMERIC_SCALE as varchar(9)) + ')'
			                    else ''
		                    end as COLUMN_DEFINITION	
                    from 
	                    INFORMATION_SCHEMA.COLUMNS c
                    where
	                    c.TABLE_NAME = '{tableName}'         
                    and
                        c.TABLE_SCHEMA = {schemaOrDefault}
                    and
	                    c.COLUMN_NAME in (select [value] from openjson(@c))
                    ";

                try
                {
                    sqlConnection.Open();
                    SqlCommand cmdColDef = new SqlCommand(columnDefinitionQuery, sqlConnection);
                    var p = cmdColDef.Parameters.Add("@c", SqlDbType.NVarChar, -1);
                    p.Value = JsonConvert.SerializeObject(columnNames);

                    using (SqlDataReader rdr = cmdColDef.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            columnsDefinition.Add(rdr["COLUMN_DEFINITION"].ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Throw a custom error so that it's easier to decipher.
                    string message = $"Encountered exception while retrieving columns definition for table '{tableName}.' Cannot generate upsert command without them.";
                    throw new InvalidOperationException(message, ex);
                }
                finally
                {
                    sqlConnection.Close();
                }

                // TODO -- do these need to be case sensitive? 
                string columnMatchingQuery = string.Empty;
                foreach (var column in columnNames)
                {
                    columnMatchingQuery += $" ExistingData.{column} = NewData.{column},";
                }

                columnMatchingQuery = columnMatchingQuery.TrimEnd(',');

                string mergeQuery = $"MERGE INTO {fullName} WITH (HOLDLOCK) AS ExistingData "
                    + "USING cte as NewData "
                    + $"ON {primaryKeyMatchingQuery}"
                    + " WHEN MATCHED THEN "
                    + $" UPDATE SET {columnMatchingQuery}"
                    + $" WHEN NOT MATCHED THEN INSERT ({string.Join(",", columnNames)}) VALUES ({string.Join(",", columnNames)})";


                // Match SQL column names to POCO field/property names
                IEnumerable<MemberInfo> primaryKeyFields = typeof(T).GetMembers().Where(f => primaryKeys.Contains(f.Name, StringComparer.OrdinalIgnoreCase));

                return new TableInformation(primaryKeyFields, columnNames, columnsDefinition, mergeQuery);
            }
        }
    }
}