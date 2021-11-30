﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <typeparam name="T">A user-defined POCO that represents a row of the user's table</typeparam>
    internal class SqlAsyncCollector<T> : IAsyncCollector<T>, IDisposable
    {
        private const string RowDataParameter = "@rowData";
        private const string ColumnName = "COLUMN_NAME";
        private const string ColumnDefinition = "COLUMN_DEFINITION";
        private const string CteName = "cte";

        private readonly IConfiguration _configuration;
        private readonly SqlAttribute _attribute;
        private readonly ILogger _logger;

        private readonly List<T> _rows = new List<T>();
        private readonly SemaphoreSlim _rowLock = new SemaphoreSlim(1, 1);

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
        /// <param name="loggerFactory">
        /// Logger Factory for creating an ILogger
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either configuration or attribute is null
        /// </exception>
        public SqlAsyncCollector(IConfiguration configuration, SqlAttribute attribute, ILoggerFactory loggerFactory)
        {
            this._configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this._attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
            this._logger = loggerFactory?.CreateLogger(LogCategories.Bindings) ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <summary>
        /// Adds an item to this collector that is processed in a batch along with all other items added via
        /// AddAsync when <see cref="FlushAsync"/> is called. Each item is interpreted as a row to be added to the SQL table
        /// specified in the SQL Binding.
        /// </summary>
        /// <param name="item"> The item to add to the collector </param>
        /// <param name="cancellationToken">The cancellationToken is not used in this method</param>
        /// <returns> A CompletedTask if executed successfully </returns>
        public async Task AddAsync(T item, CancellationToken cancellationToken = default)
        {
            if (item != null)
            {
                await this._rowLock.WaitAsync(cancellationToken);

                try
                {
                    this._rows.Add(item);
                }
                finally
                {
                    this._rowLock.Release();
                }
            }
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
            await this._rowLock.WaitAsync(cancellationToken);
            try
            {
                if (this._rows.Count != 0)
                {
                    await this.UpsertRowsAsync(this._rows, this._attribute, this._configuration);
                    this._rows.Clear();
                }
            }
            finally
            {
                this._rowLock.Release();
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
        private async Task UpsertRowsAsync(IEnumerable<T> rows, SqlAttribute attribute, IConfiguration configuration)
        {
            using SqlConnection connection = SqlBindingUtilities.BuildConnection(attribute.ConnectionStringSetting, configuration);
            string fullDatabaseAndTableName = attribute.CommandText;

            // Include the connection string hash as part of the key in case this customer has the same table in two different Sql Servers
            string cacheKey = $"{connection.ConnectionString.GetHashCode()}-{fullDatabaseAndTableName}";

            ObjectCache cachedTables = MemoryCache.Default;
            var tableInfo = cachedTables[cacheKey] as TableInformation;

            if (tableInfo == null)
            {
                tableInfo = await TableInformation.RetrieveTableInformationAsync(connection, fullDatabaseAndTableName);

                var policy = new CacheItemPolicy
                {
                    // Re-look up the primary key(s) after 10 minutes (they should not change very often!)
                    AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10)
                };

                this._logger.LogInformation($"DB and Table: {fullDatabaseAndTableName}. Primary keys: [{string.Join(",", tableInfo.PrimaryKeys.Select(pk => pk.Name))}]. SQL Column and Definitions:  [{string.Join(",", tableInfo.ColumnDefinitions)}]");
                cachedTables.Set(cacheKey, tableInfo, policy);
            }

            IEnumerable<string> extraProperties = GetExtraProperties(tableInfo.Columns);
            if (extraProperties.Any())
            {
                string message = $"The following properties in {typeof(T)} do not exist in the table {fullDatabaseAndTableName}: {string.Join(", ", extraProperties.ToArray())}.";
                throw new InvalidOperationException(message);
            }

            int batchSize = 1000;
            await connection.OpenAsync();
            SqlTransaction transaction = connection.BeginTransaction();
            try
            {
                SqlCommand command = connection.CreateCommand();
                command.Connection = connection;
                command.Transaction = transaction;
                SqlParameter par = command.Parameters.Add(RowDataParameter, SqlDbType.NVarChar, -1);

                foreach (IEnumerable<T> batch in rows.Batch(batchSize))
                {
                    GenerateDataQueryForMerge(tableInfo, batch, out string newDataQuery, out string rowData);
                    command.CommandText = $"{newDataQuery} {tableInfo.MergeQuery};";
                    par.Value = rowData;

                    await command.ExecuteNonQueryAsync();
                }
                transaction.Commit();
            }
            catch (Exception ex)
            {
                try
                {
                    transaction.Rollback();
                }
                catch (Exception ex2)
                {
                    string message2 = $"Encountered exception during upsert and rollback.";
                    throw new AggregateException(message2, new List<Exception> { ex, ex2 });
                }
                throw;
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        /// <summary>
        /// Checks if any properties in T do not exist as columns in the table
        /// to upsert to and returns the extra property names in a List.
        /// </summary>
        /// <param name="columns"> The columns of the table to upsert to </param>
        /// <returns>List of property names that don't exist in the table</returns>
        private static IEnumerable<string> GetExtraProperties(IDictionary<string, string> columns)
        {
            return typeof(T).GetProperties().ToList()
                .Where(prop => !columns.ContainsKey(prop.Name))
                .Select(prop => prop.Name);
        }

        /// <summary>
        /// Generates T-SQL for data to be upserted using Merge.
        /// This needs to be regenerated for every batch to upsert.
        /// </summary>
        /// <param name="table">Information about the table we will be upserting into</param>
        /// <param name="rows">Rows to be upserted</param>
        /// <returns>T-SQL containing data for merge</returns>
        private static void GenerateDataQueryForMerge(TableInformation table, IEnumerable<T> rows, out string newDataQuery, out string rowData)
        {
            IList<T> rowsToUpsert = new List<T>();

            // Here, we assume that primary keys are case INsensitive, which is the SQL Server default.
            var uniqueUpdatedPrimaryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // If there are duplicate primary keys, we'll need to pick the LAST (most recent) row per primary key.
            foreach (T row in rows.Reverse())
            {
                // SQL Server allows 900 bytes per primary key, so use that as a baseline
                var combinedPrimaryKey = new StringBuilder(900 * table.PrimaryKeys.Count());

                // Look up primary key of T. Because we're going in the same order of fields every time,
                // we can assume that if two rows with the same primary key are in the list, they will collide
                foreach (PropertyInfo primaryKey in table.PrimaryKeys)
                {
                    combinedPrimaryKey.Append(primaryKey.GetValue(row).ToString());
                }

                // If we have already seen this unique primary key, skip this update
                if (uniqueUpdatedPrimaryKeys.Add(combinedPrimaryKey.ToString()))
                {
                    // This is the first time we've seen this particular PK. Add this row to the upsert query.
                    rowsToUpsert.Add(row);
                }
            }

            rowData = JsonConvert.SerializeObject(rowsToUpsert, table.JsonSerializerSettings);
            IEnumerable<string> columnNamesFromPOCO = typeof(T).GetProperties().Select(prop => prop.Name);
            IEnumerable<string> bracketColumnDefinitionsFromPOCO = table.Columns.Where(c => columnNamesFromPOCO.Contains(c.Key, StringComparer.OrdinalIgnoreCase))
                .Select(c => $"{c.Key.AsBracketQuotedString()} {c.Value}");
            newDataQuery = $"WITH {CteName} AS ( SELECT * FROM OPENJSON({RowDataParameter}) WITH ({string.Join(",", bracketColumnDefinitionsFromPOCO)}) )";
        }

        public class TableInformation
        {
            public IEnumerable<MemberInfo> PrimaryKeys { get; }

            /// <summary>
            /// All of the columns, along with their data types, for SQL to use to turn JSON into a table
            /// </summary>
            public IDictionary<string, string> Columns { get; }

            /// <summary>
            /// List of strings containing each column and its type. ex: ["Cost int", "LastChangeDate datetime(7)"]
            /// </summary>
            public IEnumerable<string> ColumnDefinitions => this.Columns.Select(c => $"{c.Key} {c.Value}");

            /// <summary>
            /// T-SQL merge statement generated from primary keys
            /// and column names for a specific table.
            /// </summary>
            public string MergeQuery { get; }

            /// <summary>
            /// Settings to use when serializing the POCO into SQL.
            /// Only serialize properties and fields that correspond to SQL columns.
            /// </summary>
            public JsonSerializerSettings JsonSerializerSettings { get; }

            public TableInformation(IEnumerable<MemberInfo> primaryKeys, IDictionary<string, string> columns, string mergeQuery)
            {
                this.PrimaryKeys = primaryKeys;
                this.Columns = columns;
                this.MergeQuery = mergeQuery;

                this.JsonSerializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new DynamicPOCOContractResolver(columns)
                };
            }

            /// <summary>
            /// Generates SQL query that can be used to retrieve the Primary Keys of a table
            /// </summary>
            public static string GetPrimaryKeysQuery(string quotedSchema, string quotedTableName)
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
                        tc.TABLE_NAME = {quotedTableName}
                    and
                        tc.TABLE_SCHEMA = {quotedSchema}";
            }

            /// <summary>
            /// Generates SQL query that can be used to retrieve column names & types of a table
            /// </summary>
            public static string GetColumnDefinitionsQuery(string quotedSchema, string quotedTableName)
            {
                return $@"
                    select
	                    {ColumnName}, DATA_TYPE +
		                    case
			                    when CHARACTER_MAXIMUM_LENGTH = -1 then '(max)'
			                    when CHARACTER_MAXIMUM_LENGTH <> -1 then '(' + cast(CHARACTER_MAXIMUM_LENGTH as varchar(4)) + ')'
                                when DATETIME_PRECISION is not null and DATA_TYPE not in ('datetime', 'date', 'smalldatetime') then '(' + cast(DATETIME_PRECISION as varchar(1)) + ')'
			                    when DATA_TYPE in ('decimal', 'numeric') then '(' + cast(NUMERIC_PRECISION as varchar(9)) + ',' + + cast(NUMERIC_SCALE as varchar(9)) + ')'
			                    else ''
		                    end as {ColumnDefinition}
                    from
	                    INFORMATION_SCHEMA.COLUMNS c
                    where
	                    c.TABLE_NAME = {quotedTableName}
                    and
                        c.TABLE_SCHEMA = {quotedSchema}";
            }

            /// <summary>
            /// Generates reusable SQL query that will be part of every upsert command.
            /// </summary>
            public static string GetMergeQuery(IList<string> primaryKeys, string fullTableName)
            {
                IList<string> bracketedPrimaryKeys = primaryKeys.Select(p => p.AsBracketQuotedString()).ToList();
                // Generate the ON part of the merge query (compares new data against existing data)
                var primaryKeyMatchingQuery = new StringBuilder($"ExistingData.{bracketedPrimaryKeys[0]} = NewData.{bracketedPrimaryKeys[0]}");
                foreach (string primaryKey in bracketedPrimaryKeys.Skip(1))
                {
                    primaryKeyMatchingQuery.Append($" AND ExistingData.{primaryKey} = NewData.{primaryKey}");
                }

                // Generate the UPDATE part of the merge query (all columns that should be updated)
                IEnumerable<string> bracketedColumnNamesFromPOCO = typeof(T).GetProperties().Select(prop => prop.Name.ToLowerInvariant().AsBracketQuotedString());
                var columnMatchingQueryBuilder = new StringBuilder();
                foreach (string column in bracketedColumnNamesFromPOCO)
                {
                    columnMatchingQueryBuilder.Append($" ExistingData.{column} = NewData.{column},");
                }

                string columnMatchingQuery = columnMatchingQueryBuilder.ToString().TrimEnd(',');
                return @$"
                    MERGE INTO {fullTableName} WITH (HOLDLOCK)
                        AS ExistingData
                    USING {CteName}
                        AS NewData
                    ON
                        {primaryKeyMatchingQuery}
                    WHEN MATCHED THEN
                        UPDATE SET {columnMatchingQuery}
                    WHEN NOT MATCHED THEN
                        INSERT ({string.Join(",", bracketedColumnNamesFromPOCO)}) VALUES ({string.Join(",", bracketedColumnNamesFromPOCO)})";
            }

            /// <summary>
            /// Retrieve (relatively) static information of SQL Table like primary keys, column names, etc.
            /// in order to generate the MERGE portion of the upsert query.
            /// This only needs to be generated once and can be reused for subsequent upserts.
            /// </summary>
            /// <param name="sqlConnection">Connection with which to query SQL against</param>
            /// <param name="fullName">Full name of table, including schema (if exists).</param>
            /// <returns>TableInformation object containing primary keys, column types, etc.</returns>
            public static async Task<TableInformation> RetrieveTableInformationAsync(SqlConnection sqlConnection, string fullName)
            {
                SqlBindingUtilities.GetTableAndSchema(fullName, out string quotedSchema, out string quotedTableName);

                // 1. Get all column names and types
                var columnDefinitionsFromSQL = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    await sqlConnection.OpenAsync();
                    var cmdColDef = new SqlCommand(GetColumnDefinitionsQuery(quotedSchema, quotedTableName), sqlConnection);
                    using SqlDataReader rdr = await cmdColDef.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        columnDefinitionsFromSQL.Add(rdr[ColumnName].ToString().ToLowerInvariant(), rdr[ColumnDefinition].ToString());
                    }
                }
                catch (Exception ex)
                {
                    // Throw a custom error so that it's easier to decipher.
                    string message = $"Encountered exception while retrieving column names and types for table {quotedTableName} in schema {quotedSchema}. Cannot generate upsert command without them.";
                    throw new InvalidOperationException(message, ex);
                }
                finally
                {
                    await sqlConnection.CloseAsync();
                }

                if (columnDefinitionsFromSQL.Count == 0)
                {
                    string message = $"Table {quotedTableName} in schema {quotedSchema} does not exist.";
                    throw new InvalidOperationException(message);
                }

                // 2. Query SQL for table Primary Keys
                var primaryKeys = new List<string>();
                try
                {
                    await sqlConnection.OpenAsync();
                    var cmd = new SqlCommand(GetPrimaryKeysQuery(quotedSchema, quotedTableName), sqlConnection);
                    using SqlDataReader rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        primaryKeys.Add(rdr[ColumnName].ToString().ToLowerInvariant());
                    }
                }
                catch (Exception ex)
                {
                    // Throw a custom error so that it's easier to decipher.
                    string message = $"Encountered exception while retrieving primary keys for table {quotedTableName} in schema {quotedSchema}. Cannot generate upsert command without them.";
                    throw new InvalidOperationException(message, ex);
                }
                finally
                {
                    await sqlConnection.CloseAsync();
                }

                if (!primaryKeys.Any())
                {
                    string message = $"Did not retrieve any primary keys for {quotedTableName} in schema {quotedSchema}. Cannot generate upsert command without them.";
                    throw new InvalidOperationException(message);
                }

                // 3. Match SQL Primary Key column names to POCO field/property objects. Ensure none are missing.
                IEnumerable<MemberInfo> primaryKeyFields = typeof(T).GetMembers().Where(f => primaryKeys.Contains(f.Name, StringComparer.OrdinalIgnoreCase));
                IEnumerable<string> primaryKeysFromPOCO = primaryKeyFields.Select(f => f.Name);
                IEnumerable<string> missingFromPOCO = primaryKeys.Except(primaryKeysFromPOCO, StringComparer.OrdinalIgnoreCase);
                if (missingFromPOCO.Any())
                {
                    string message = $"All primary keys for SQL table {quotedTableName} and schema {quotedSchema} need to be found in '{typeof(T)}.' Missing primary keys: [{string.Join(",", missingFromPOCO)}]";
                    throw new InvalidOperationException(message);
                }

                return new TableInformation(primaryKeyFields, columnDefinitionsFromSQL, GetMergeQuery(primaryKeys, fullName));
            }
        }

        public class DynamicPOCOContractResolver : DefaultContractResolver
        {
            private readonly IDictionary<string, string> _propertiesToSerialize;

            public DynamicPOCOContractResolver(IDictionary<string, string> sqlColumns)
            {
                // we only want to serialize POCO properties that correspond to SQL columns
                this._propertiesToSerialize = sqlColumns;
            }

            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                var properties = base
                    .CreateProperties(type, memberSerialization)
                    .ToDictionary(p => p.PropertyName, StringComparer.OrdinalIgnoreCase);

                // Make sure the ordering of columns matches that of SQL
                // Necessary for proper matching of column names to JSON that is generated for each batch of data
                IList<JsonProperty> propertiesToSerialize = new List<JsonProperty>(properties.Count);
                foreach (KeyValuePair<string, string> column in this._propertiesToSerialize)
                {
                    if (properties.ContainsKey(column.Key))
                    {
                        // Lower-case the property name during serialization to match SQL casing
                        JsonProperty sqlColumn = properties[column.Key];
                        sqlColumn.PropertyName = sqlColumn.PropertyName.ToLowerInvariant();
                        propertiesToSerialize.Add(sqlColumn);
                    }
                }

                return propertiesToSerialize;
            }
        }

        public void Dispose()
        {
            this._rowLock.Dispose();
        }
    }
}