// Copyright (c) Microsoft Corporation. All rights reserved.
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
using static Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry.Telemetry;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry;
using System.Diagnostics;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{

    internal class PrimaryKey
    {
        public readonly string Name;

        public readonly bool IsIdentity;

        public PrimaryKey(string name, bool isIdentity)
        {
            this.Name = name;
            this.IsIdentity = isIdentity;
        }
    }

    /// <typeparam name="T">A user-defined POCO that represents a row of the user's table</typeparam>
    internal class SqlAsyncCollector<T> : IAsyncCollector<T>, IDisposable
    {
        private const string RowDataParameter = "@rowData";
        private const string ColumnName = "COLUMN_NAME";
        private const string ColumnDefinition = "COLUMN_DEFINITION";

        private const string IsIdentity = "is_identity";
        private const string CteName = "cte";

        private const string Collation = "Collation";

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
            TelemetryInstance.TrackCreate(CreateType.SqlAsyncCollector);
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
                TelemetryInstance.TrackEvent(TelemetryEventName.AddAsync);
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
                    TelemetryInstance.TrackEvent(TelemetryEventName.FlushAsync);
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
            await connection.OpenAsync();
            Dictionary<string, string> props = connection.AsConnectionProps();

            string fullTableName = attribute.CommandText;

            // Include the connection string hash as part of the key in case this customer has the same table in two different Sql Servers
            string cacheKey = $"{connection.ConnectionString.GetHashCode()}-{fullTableName}";

            ObjectCache cachedTables = MemoryCache.Default;
            var tableInfo = cachedTables[cacheKey] as TableInformation;

            if (tableInfo == null)
            {
                TelemetryInstance.TrackEvent(TelemetryEventName.TableInfoCacheMiss, props);
                tableInfo = await TableInformation.RetrieveTableInformationAsync(connection, fullTableName, this._logger);
                var policy = new CacheItemPolicy
                {
                    // Re-look up the primary key(s) after 10 minutes (they should not change very often!)
                    AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10)
                };

                this._logger.LogInformation($"DB and Table: {connection.Database}.{fullTableName}. Primary keys: [{string.Join(",", tableInfo.PrimaryKeys.Select(pk => pk.Name))}]. SQL Column and Definitions:  [{string.Join(",", tableInfo.ColumnDefinitions)}]");
                cachedTables.Set(cacheKey, tableInfo, policy);
            }
            else
            {
                TelemetryInstance.TrackEvent(TelemetryEventName.TableInfoCacheHit, props);
            }

            IEnumerable<string> extraProperties = GetExtraProperties(tableInfo.Columns);
            if (extraProperties.Any())
            {
                string message = $"The following properties in {typeof(T)} do not exist in the table {fullTableName}: {string.Join(", ", extraProperties.ToArray())}.";
                var ex = new InvalidOperationException(message);
                TelemetryInstance.TrackError(TelemetryErrorName.PropsNotExistOnTable, ex, props);
                throw ex;
            }

            TelemetryInstance.TrackEvent(TelemetryEventName.UpsertStart, props);
            var transactionSw = Stopwatch.StartNew();
            int batchSize = 1000;
            SqlTransaction transaction = connection.BeginTransaction();
            try
            {
                SqlCommand command = connection.CreateCommand();
                command.Connection = connection;
                command.Transaction = transaction;
                SqlParameter par = command.Parameters.Add(RowDataParameter, SqlDbType.NVarChar, -1);
                int batchCount = 0;
                var commandSw = Stopwatch.StartNew();
                foreach (IEnumerable<T> batch in rows.Batch(batchSize))
                {
                    batchCount++;
                    GenerateDataQueryForMerge(tableInfo, batch, out string newDataQuery, out string rowData);
                    command.CommandText = $"{newDataQuery} {tableInfo.Query};";
                    par.Value = rowData;
                    await command.ExecuteNonQueryAsync();
                }
                transaction.Commit();
                var measures = new Dictionary<string, double>()
                {
                    { TelemetryMeasureName.BatchCount.ToString(), batchCount },
                    { TelemetryMeasureName.TransactionDurationMs.ToString(), transactionSw.ElapsedMilliseconds },
                    { TelemetryMeasureName.CommandDurationMs.ToString(), commandSw.ElapsedMilliseconds }
                };
                TelemetryInstance.TrackEvent(TelemetryEventName.UpsertEnd, props, measures);
            }
            catch (Exception ex)
            {
                try
                {
                    TelemetryInstance.TrackError(TelemetryErrorName.Upsert, ex, props);
                    transaction.Rollback();
                }
                catch (Exception ex2)
                {
                    TelemetryInstance.TrackError(TelemetryErrorName.UpsertRollback, ex2, props);
                    string message2 = $"Encountered exception during upsert and rollback.";
                    throw new AggregateException(message2, new List<Exception> { ex, ex2 });
                }
                throw;
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

            var uniqueUpdatedPrimaryKeys = new HashSet<string>(table.Comparer);

            // If there are duplicate primary keys, we'll need to pick the LAST (most recent) row per primary key.
            foreach (T row in rows.Reverse())
            {
                // SQL Server allows 900 bytes per primary key, so use that as a baseline
                var combinedPrimaryKey = new StringBuilder(900 * table.PrimaryKeys.Count());

                // Look up primary key of T. Because we're going in the same order of fields every time,
                // we can assume that if two rows with the same primary key are in the list, they will collide
                foreach (PropertyInfo primaryKey in table.PrimaryKeys)
                {
                    object value = primaryKey.GetValue(row);
                    // Identity columns are allowed to be optional, so just skip the key if it doesn't exist
                    if (value == null)
                    {
                        continue;
                    }
                    combinedPrimaryKey.Append(value.ToString());
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
            IEnumerable<string> bracketColumnDefinitionsFromPOCO = table.Columns.Where(c => columnNamesFromPOCO.Contains(c.Key, table.Comparer))
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
            /// The StringComparer to use when comparing column names. Ex. StringComparer.Ordinal or StringComparer.OrdinalIgnoreCase
            /// </summary>
            public StringComparer Comparer { get; }

            /// <summary>
            /// T-SQL merge statement generated from primary keys
            /// T-SQL merge or insert statement generated from primary keys
            /// and column names for a specific table.
            /// </summary>
            public string Query { get; }

            /// <summary>
            /// Settings to use when serializing the POCO into SQL.
            /// Only serialize properties and fields that correspond to SQL columns.
            /// </summary>
            public JsonSerializerSettings JsonSerializerSettings { get; }

            public TableInformation(IEnumerable<MemberInfo> primaryKeys, IDictionary<string, string> columns, StringComparer comparer, string query)
            {
                this.PrimaryKeys = primaryKeys;
                this.Columns = columns;
                this.Comparer = comparer;
                this.Query = query;

                this.JsonSerializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new DynamicPOCOContractResolver(columns, comparer)
                };
            }

            public static bool GetCaseSensitivityFromCollation(string collation)
            {
                return collation.Contains("_CS_");
            }

            public static string GetDatabaseCollationQuery(SqlConnection sqlConnection)
            {
                return $@"
                    SELECT 
                        DATABASEPROPERTYEX('{sqlConnection.Database}', '{Collation}') AS {Collation};";
            }

            /// <summary>
            /// Generates SQL query that can be used to retrieve the Primary Keys of a table
            /// </summary>
            public static string GetPrimaryKeysQuery(SqlObject table)
            {
                return $@"
                    SELECT
                        {ColumnName}, c.is_identity
                    FROM
                        INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    INNER JOIN
                        INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE ccu ON ccu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME AND ccu.TABLE_NAME = tc.TABLE_NAME
                    INNER JOIN
                        sys.columns c ON c.object_id = OBJECT_ID({table.QuotedFullName}) AND c.name = ccu.COLUMN_NAME
                    WHERE
                        tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                    and
                        tc.TABLE_NAME = {table.QuotedName}
                    and
                        tc.TABLE_SCHEMA = {table.QuotedSchema}";
            }

            /// <summary>
            /// Generates SQL query that can be used to retrieve column names & types of a table
            /// </summary>
            public static string GetColumnDefinitionsQuery(SqlObject table)
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
	                    c.TABLE_NAME = {table.QuotedName}
                    and
                        c.TABLE_SCHEMA = {table.QuotedSchema}";
            }

            public static string GetInsertQuery(SqlObject table)
            {
                IEnumerable<string> bracketedColumnNamesFromPOCO = typeof(T).GetProperties().Select(prop => prop.Name.AsBracketQuotedString());
                return $"INSERT INTO {table.BracketQuotedFullName} SELECT * FROM {CteName}";
            }

            /// <summary>
            /// Generates reusable SQL query that will be part of every upsert command.
            /// </summary>
            public static string GetMergeQuery(IList<PrimaryKey> primaryKeys, SqlObject table, StringComparison comparison)
            {
                IList<string> bracketedPrimaryKeys = primaryKeys.Select(p => p.Name.AsBracketQuotedString()).ToList();
                // Generate the ON part of the merge query (compares new data against existing data)
                var primaryKeyMatchingQuery = new StringBuilder($"ExistingData.{bracketedPrimaryKeys[0]} = NewData.{bracketedPrimaryKeys[0]}");
                foreach (string primaryKey in bracketedPrimaryKeys.Skip(1))
                {
                    primaryKeyMatchingQuery.Append($" AND ExistingData.{primaryKey} = NewData.{primaryKey}");
                }

                // Generate the UPDATE part of the merge query (all columns that should be updated)
                IEnumerable<string> bracketedColumnNamesFromPOCO = typeof(T).GetProperties()
                    .Where(prop => !primaryKeys.Any(k => k.IsIdentity && string.Equals(k.Name, prop.Name, comparison))) // Skip any identity columns, those should never be updated
                    .Select(prop => prop.Name.AsBracketQuotedString());
                var columnMatchingQueryBuilder = new StringBuilder();
                foreach (string column in bracketedColumnNamesFromPOCO)
                {
                    columnMatchingQueryBuilder.Append($" ExistingData.{column} = NewData.{column},");
                }

                string columnMatchingQuery = columnMatchingQueryBuilder.ToString().TrimEnd(',');
                return @$"
                    MERGE INTO {table.BracketQuotedFullName} WITH (HOLDLOCK)
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
            /// <param name="sqlConnection">An open connection with which to query SQL against</param>
            /// <param name="fullName">Full name of table, including schema (if exists).</param>
            /// <param name="logger">ILogger used to log any errors or warnings.</param>
            /// <returns>TableInformation object containing primary keys, column types, etc.</returns>
            public static async Task<TableInformation> RetrieveTableInformationAsync(SqlConnection sqlConnection, string fullName, ILogger logger)
            {
                Dictionary<string, string> sqlConnProps = sqlConnection.AsConnectionProps();
                TelemetryInstance.TrackEvent(TelemetryEventName.GetTableInfoStart, sqlConnProps);
                var table = new SqlObject(fullName);

                // Get case sensitivity from database collation (default to false if any exception occurs)
                bool caseSensitive = false;
                var tableInfoSw = Stopwatch.StartNew();
                var caseSensitiveSw = Stopwatch.StartNew();
                try
                {
                    var cmdCollation = new SqlCommand(GetDatabaseCollationQuery(sqlConnection), sqlConnection);
                    using SqlDataReader rdr = await cmdCollation.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        caseSensitive = GetCaseSensitivityFromCollation(rdr[Collation].ToString());
                    }
                    caseSensitiveSw.Stop();
                    TelemetryInstance.TrackDuration(TelemetryEventName.GetCaseSensitivity, caseSensitiveSw.ElapsedMilliseconds, sqlConnProps);
                }
                catch (Exception ex)
                {
                    // Since this doesn't rethrow make sure we stop here too (don't use finally because we want the execution time to be the same here and in the 
                    // overall event but we also only want to send the GetCaseSensitivity event if it succeeds)
                    caseSensitiveSw.Stop();
                    TelemetryInstance.TrackError(TelemetryErrorName.GetCaseSensitivity, ex, sqlConnProps);
                    logger.LogWarning($"Encountered exception while retrieving database collation: {ex}. Case insensitive behavior will be used by default.");
                }

                StringComparer comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

                // Get all column names and types
                var columnDefinitionsFromSQL = new Dictionary<string, string>(comparer);
                var columnDefinitionsSw = Stopwatch.StartNew();
                try
                {
                    var cmdColDef = new SqlCommand(GetColumnDefinitionsQuery(table), sqlConnection);
                    using SqlDataReader rdr = await cmdColDef.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        string columnName = caseSensitive ? rdr[ColumnName].ToString() : rdr[ColumnName].ToString().ToLowerInvariant();
                        columnDefinitionsFromSQL.Add(columnName, rdr[ColumnDefinition].ToString());
                    }
                    columnDefinitionsSw.Stop();
                    TelemetryInstance.TrackDuration(TelemetryEventName.GetColumnDefinitions, columnDefinitionsSw.ElapsedMilliseconds, sqlConnProps);
                }
                catch (Exception ex)
                {
                    TelemetryInstance.TrackError(TelemetryErrorName.GetColumnDefinitions, ex, sqlConnProps);
                    // Throw a custom error so that it's easier to decipher.
                    string message = $"Encountered exception while retrieving column names and types for table {table}. Cannot generate upsert command without them.";
                    throw new InvalidOperationException(message, ex);
                }

                if (columnDefinitionsFromSQL.Count == 0)
                {
                    string message = $"Table {table} does not exist.";
                    var ex = new InvalidOperationException(message);
                    TelemetryInstance.TrackError(TelemetryErrorName.GetColumnDefinitionsTableDoesNotExist, ex, sqlConnProps);
                    throw ex;
                }

                // Query SQL for table Primary Keys
                var primaryKeys = new List<PrimaryKey>();
                var primaryKeysSw = Stopwatch.StartNew();
                try
                {
                    var cmd = new SqlCommand(GetPrimaryKeysQuery(table), sqlConnection);
                    using SqlDataReader rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        string columnName = caseSensitive ? rdr[ColumnName].ToString() : rdr[ColumnName].ToString().ToLowerInvariant();
                        primaryKeys.Add(new PrimaryKey(columnName, bool.Parse(rdr[IsIdentity].ToString())));
                    }
                    primaryKeysSw.Stop();
                    TelemetryInstance.TrackDuration(TelemetryEventName.GetPrimaryKeys, primaryKeysSw.ElapsedMilliseconds, sqlConnProps);
                }
                catch (Exception ex)
                {
                    TelemetryInstance.TrackError(TelemetryErrorName.GetPrimaryKeys, ex, sqlConnProps);
                    // Throw a custom error so that it's easier to decipher.
                    string message = $"Encountered exception while retrieving primary keys for table {table}. Cannot generate upsert command without them.";
                    throw new InvalidOperationException(message, ex);
                }

                if (!primaryKeys.Any())
                {
                    string message = $"Did not retrieve any primary keys for {table}. Cannot generate upsert command without them.";
                    var ex = new InvalidOperationException(message);
                    TelemetryInstance.TrackError(TelemetryErrorName.NoPrimaryKeys, ex, sqlConnProps);
                    throw ex;
                }

                // Match SQL Primary Key column names to POCO field/property objects. Ensure none are missing.
                StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                IEnumerable<MemberInfo> primaryKeyFields = typeof(T).GetMembers().Where(f => primaryKeys.Any(k => string.Equals(k.Name, f.Name, comparison)));
                IEnumerable<string> primaryKeysFromPOCO = primaryKeyFields.Select(f => f.Name);
                IEnumerable<PrimaryKey> missingPrimaryKeysFromPOCO = primaryKeys
                    .Where(k => !primaryKeysFromPOCO.Contains(k.Name, comparer));
                bool hasIdentityColumnPrimaryKeys = primaryKeys.Any(k => k.IsIdentity);
                // If none of the primary keys are an identity column then we require that all primary keys be present in the POCO so we can
                // generate the MERGE statement correctly
                if (!hasIdentityColumnPrimaryKeys && missingPrimaryKeysFromPOCO.Any())
                {
                    string message = $"All primary keys for SQL table {table} need to be found in '{typeof(T)}.' Missing primary keys: [{string.Join(",", missingPrimaryKeysFromPOCO)}]";
                    var ex = new InvalidOperationException(message);
                    TelemetryInstance.TrackError(TelemetryErrorName.MissingPrimaryKeys, ex, sqlConnProps);
                    throw ex;
                }

                // If any identity columns aren't included in the object then we have to generate a basic insert since the merge statement expects all primary key
                // columns to exist. (the merge statement can handle nullable columns though if those exist)
                bool usingInsertQuery = hasIdentityColumnPrimaryKeys && missingPrimaryKeysFromPOCO.Any();
                string query = usingInsertQuery ? GetInsertQuery(table) : GetMergeQuery(primaryKeys, table, comparison);

                tableInfoSw.Stop();
                var durations = new Dictionary<string, double>()
                {
                    { TelemetryMeasureName.GetCaseSensitivityDurationMs.ToString(), caseSensitiveSw.ElapsedMilliseconds },
                    { TelemetryMeasureName.GetColumnDefinitionsDurationMs.ToString(), columnDefinitionsSw.ElapsedMilliseconds },
                    { TelemetryMeasureName.GetPrimaryKeysDurationMs.ToString(), primaryKeysSw.ElapsedMilliseconds }
                };
                sqlConnProps.Add(TelemetryPropertyName.QueryType.ToString(), usingInsertQuery ? "insert" : "merge");
                sqlConnProps.Add(TelemetryPropertyName.HasIdentityColumn.ToString(), hasIdentityColumnPrimaryKeys.ToString());
                TelemetryInstance.TrackDuration(TelemetryEventName.GetTableInfoEnd, tableInfoSw.ElapsedMilliseconds, sqlConnProps, durations);
                return new TableInformation(primaryKeyFields, columnDefinitionsFromSQL, comparer, query);
            }
        }

        public class DynamicPOCOContractResolver : DefaultContractResolver
        {
            private readonly IDictionary<string, string> _propertiesToSerialize;
            private readonly StringComparer _comparer;

            public DynamicPOCOContractResolver(IDictionary<string, string> sqlColumns, StringComparer comparer)
            {
                // we only want to serialize POCO properties that correspond to SQL columns
                this._propertiesToSerialize = sqlColumns;
                this._comparer = comparer;
            }

            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                var properties = base
                    .CreateProperties(type, memberSerialization)
                    .ToDictionary(p => p.PropertyName, this._comparer);

                // Make sure the ordering of columns matches that of SQL
                // Necessary for proper matching of column names to JSON that is generated for each batch of data
                IList<JsonProperty> propertiesToSerialize = new List<JsonProperty>(properties.Count);
                foreach (KeyValuePair<string, string> column in this._propertiesToSerialize)
                {
                    if (properties.ContainsKey(column.Key))
                    {
                        JsonProperty sqlColumn = properties[column.Key];
                        sqlColumn.PropertyName = this._comparer == StringComparer.Ordinal ? sqlColumn.PropertyName : sqlColumn.PropertyName.ToLowerInvariant();
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