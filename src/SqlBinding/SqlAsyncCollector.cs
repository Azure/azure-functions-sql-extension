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
using System.Security.Cryptography;
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
        private static string RowDataParameter = "@rowData";
        private static string ColumnName = "COLUMN_NAME";
        private static string ColumnDefinition = "COLUMN_DEFINITION";
        private static string NewDataParameter = "cte";

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
            using (SqlConnection connection = SqlBindingUtilities.BuildConnection(attribute.ConnectionStringSetting, configuration))
            {
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

                int batchSize = 1000;
                await connection.OpenAsync();
                foreach (IEnumerable<T> batch in rows.Batch(batchSize))
                {
                    GenerateDataQueryForMerge(tableInfo, batch, out string newDataQuery, out string rowData);
                    var cmd = new SqlCommand($"{newDataQuery} {tableInfo.MergeQuery};", connection);
                    var par = cmd.Parameters.Add(RowDataParameter, SqlDbType.NVarChar, -1);
                    par.Value = rowData;

                    await cmd.ExecuteNonQueryAsync();
                }
                await connection.CloseAsync();
            }
        }
        /// <summary>
        ///  Generates T-SQL for data to be upserted using Merge
        /// </summary>
        /// <param name="table">Information about the table we will be upserting into</param>
        /// <param name="rows">Rows to be upserted</param>
        /// <returns>T-SQL containing data for merge</returns>
        private static void GenerateDataQueryForMerge(TableInformation table, IEnumerable<T> rows, out string newDataQuery, out string rowData)
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

            rowData = JsonConvert.SerializeObject(rowsToUpsert);
            newDataQuery = $"WITH {NewDataParameter} AS ( SELECT * FROM OPENJSON({RowDataParameter}) WITH ({string.Join(",", table.ColumnDefinitions)}) )";
        }

        public class TableInformation
        {
            public IEnumerable<MemberInfo> PrimaryKeys { get; }

            /// <summary>
            /// List of all of the columns, along with their data types, to use to turn JSON into table
            /// </summary>
            public IDictionary<string, string> Columns { get; }

            /// <summary>
            /// List of strings containing each column and its type. ex: ["Cost int", "LastChangeDate datetime(7)"]
            /// </summary>
            public IEnumerable<string> ColumnDefinitions => Columns.Select(c => $"{c.Key} {c.Value}");

            /// <summary>
            /// T-SQL merge statement generated from primary keys
            /// and column names for a specific table.
            /// </summary>
            public string MergeQuery { get; }

            public TableInformation(IEnumerable<MemberInfo> primaryKeys, IDictionary<string, string> columns, string mergeQuery)
            {
                this.PrimaryKeys = primaryKeys;
                this.Columns = columns;
                this.MergeQuery = mergeQuery;
            }

            /// <summary>
            /// Generates SQL query that can be used to retrieve the Primary Keys of a table
            /// </summary>
            public static string GetPrimaryKeysQuery(string schema, string tableName) 
            {
                return $@"
                    select 
                        {ColumnName}
                    from
                        INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    inner join
                        INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE ccu on ccu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME AND ccu.TABLE_NAME = tc.TABLE_NAME
                    where
                        tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                    and
                        tc.TABLE_NAME = '{tableName}'
                    and
                        tc.TABLE_SCHEMA = {schema}";
            }

            /// <summary>
            /// Generates SQL query that can be used to retrieve column names & types of a table
            /// </summary>
            public static string GetColumnDefinitionsQuery(string schema, string tableName)
            {
                return $@"
                    select	
	                    {ColumnName}, DATA_TYPE +
		                    case 
			                    when CHARACTER_MAXIMUM_LENGTH = -1 then '(max)'
			                    when CHARACTER_MAXIMUM_LENGTH <> -1 then '(' + cast(CHARACTER_MAXIMUM_LENGTH as varchar(4)) + ')'
                                when DATETIME_PRECISION is not null then '(' + cast(DATETIME_PRECISION as varchar(1)) + ')'
			                    when DATA_TYPE in ('decimal', 'numeric') then '(' + cast(NUMERIC_PRECISION as varchar(9)) + ',' + + cast(NUMERIC_SCALE as varchar(9)) + ')'
			                    else ''
		                    end as {ColumnDefinition}	
                    from 
	                    INFORMATION_SCHEMA.COLUMNS c
                    where
	                    c.TABLE_NAME = '{tableName}'         
                    and
                        c.TABLE_SCHEMA = {schema}";
            }

            /// <summary>
            /// Generates reusable SQL query that will be part of every upsert command.
            /// </summary>
            public static string GetMergeQuery(IList<string> primaryKeys, IEnumerable<string> columnNames, string fullTableName)
            {
                // Generate the ON part of the merge query (compares new data against existing data)
                string primaryKeyMatchingQuery = $"ExistingData.{primaryKeys[0]} = NewData.{primaryKeys[0]}";
                foreach (var primaryKey in primaryKeys.Skip(1))
                {
                    primaryKeyMatchingQuery += $" AND ExistingData.{primaryKey} = NewData.{primaryKey}";
                }

                // Generate the UPDATE part of the merge query (all columns that should be updated)
                string columnMatchingQuery = string.Empty;
                foreach (var column in columnNames)
                {
                    columnMatchingQuery += $" ExistingData.{column} = NewData.{column},";
                }
                columnMatchingQuery = columnMatchingQuery.TrimEnd(',');

                return @$"
                    MERGE INTO {fullTableName} WITH (HOLDLOCK) 
                        AS ExistingData
                    USING {NewDataParameter}
                        AS NewData
                    ON 
                        {primaryKeyMatchingQuery}
                    WHEN MATCHED THEN
                        UPDATE SET {columnMatchingQuery}
                    WHEN NOT MATCHED THEN 
                        INSERT ({string.Join(",", columnNames)}) VALUES ({string.Join(",", columnNames)})";
            }

            /// <summary>
            /// Retrieve (relatively) static information of SQL Table like primary keys, column names, etc. 
            /// This information is used to generate the reusable portion of the MERGE query. 
            /// </summary>
            /// <param name="sqlConnection">Connection with which to query SQL against</param>
            /// <param name="fullName">Full name of table, including schema (if exists).</param>
            /// <returns>TableInformation object containing primary keys, column types, etc.</returns>
            public static TableInformation RetrieveTableInformation(SqlConnection sqlConnection, string fullName)
            {
                SqlBindingUtilities.GetTableAndSchema(fullName, out string schema, out string tableName);

                // 1. Get all column names and types
                var columnDefinitionsFromSQL = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    sqlConnection.Open();
                    SqlCommand cmdColDef = new SqlCommand(GetColumnDefinitionsQuery(schema, tableName), sqlConnection);
                    using (SqlDataReader rdr = cmdColDef.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            columnDefinitionsFromSQL.Add(rdr[ColumnName].ToString(), rdr[ColumnDefinition].ToString());
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

                // 2. Ensure the POCO fields match the SQL column names. If not, throw. (TODO: is this expected behavior?)
                IEnumerable<string> columnNamesFromPOCO = typeof(T).GetMembers().Where(f => f.MemberType == MemberTypes.Property || f.MemberType == MemberTypes.Field).Select(f => f.Name);
                IEnumerable<string> columnNamesFromSQL = columnDefinitionsFromSQL.Select(c => c.Key);

                IEnumerable<string> missingFromSQL = columnNamesFromPOCO.Except(columnNamesFromSQL, StringComparer.OrdinalIgnoreCase);
                IEnumerable<string> missingFromPOCO = columnNamesFromSQL.Except(columnNamesFromPOCO, StringComparer.OrdinalIgnoreCase);
                if (missingFromSQL.Any() || missingFromPOCO.Any())
                {
                    string sqlWarning = missingFromSQL.Any() ? $" Columns missing from SQL: [{string.Join(",", missingFromSQL)}]." : string.Empty;
                    string pocoWarning = missingFromPOCO.Any() ? $" Columns missing from '{typeof(T)}': [{string.Join(",", missingFromPOCO)}]" : string.Empty;
                    string message = $"Expect exact match between fields of '{typeof(T)}' and SQL table '{tableName}.'{sqlWarning}{pocoWarning}";
                    throw new InvalidOperationException(message);
                }

                // 3. Make sure the ordering of columns matches that of the POCO (if they differ)
                // Necessary for proper matching of column names to JSON that is generated for each batch of data
                Dictionary<string, string> columnDataMatchPOCO = new Dictionary<string, string>(columnNamesFromPOCO.Count(), StringComparer.OrdinalIgnoreCase);
                foreach (string pocoColumn in columnNamesFromPOCO)
                {
                    columnDataMatchPOCO.Add(pocoColumn, columnDefinitionsFromSQL[pocoColumn]);
                }

                // 4. Query SQL for table Primary Keys
                var primaryKeys = new List<string>();
                try
                {
                    sqlConnection.Open();
                    SqlCommand cmd = new SqlCommand(GetPrimaryKeysQuery(schema, tableName), sqlConnection);
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            primaryKeys.Add(rdr[ColumnName].ToString());
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

                // Match column names to POCO field/property objects
                IEnumerable<MemberInfo> primaryKeyFields = typeof(T).GetMembers().Where(f => primaryKeys.Contains(f.Name, StringComparer.OrdinalIgnoreCase));

                return new TableInformation(primaryKeyFields, columnDataMatchPOCO, GetMergeQuery(primaryKeys, columnNamesFromPOCO, fullName));
            }
        }
    }
}