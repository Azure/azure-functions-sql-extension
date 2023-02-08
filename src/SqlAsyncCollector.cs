// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlBindingConstants;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlBindingUtilities;
using static Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry.Telemetry;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{

    internal class PrimaryKey
    {
        public readonly string Name;

        public readonly bool IsIdentity;

        public readonly bool HasDefault;

        public PrimaryKey(string name, bool isIdentity, bool hasDefault)
        {
            this.Name = name;
            this.IsIdentity = isIdentity;
            this.HasDefault = hasDefault;
        }

        public override string ToString()
        {
            return this.Name;
        }
    }

    public enum QueryType
    {
        Insert,
        Merge
    }

    /// <typeparam name="T">A user-defined POCO that represents a row of the user's table</typeparam>
    internal class SqlAsyncCollector<T> : IAsyncCollector<T>, IDisposable
    {
        private static readonly string[] UnsupportedTypes = { "NTEXT(*)", "TEXT(*)", "IMAGE(*)" };
        private const string RowDataParameter = "@rowData";
        private const string ColumnName = "COLUMN_NAME";
        private const string ColumnDefinition = "COLUMN_DEFINITION";

        private const string HasDefault = "has_default";
        private const string IsIdentity = "is_identity";
        private const string CteName = "cte";

        private const int AZ_FUNC_TABLE_INFO_CACHE_TIMEOUT_MINUTES = 10;

        private readonly IConfiguration _configuration;
        private readonly SqlAttribute _attribute;
        private readonly ILogger _logger;

        private readonly List<T> _rows = new List<T>();
        private readonly SemaphoreSlim _rowLock = new SemaphoreSlim(1, 1);
        private ServerProperties _serverProperties;

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
        public SqlAsyncCollector(IConfiguration configuration, SqlAttribute attribute, ILogger logger)
        {
            this._configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this._attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
            this._logger = logger;
            TelemetryInstance.TrackCreate(CreateType.SqlAsyncCollector);
            using (SqlConnection connection = BuildConnection(attribute.ConnectionStringSetting, configuration))
            {
                this._logger.LogDebugWithThreadId("BEGIN OpenSqlAsyncCollectorVerifyDatabaseSupportedConnection");
                connection.OpenAsyncWithSqlErrorHandling(CancellationToken.None).Wait();
                this._logger.LogDebugWithThreadId("END OpenSqlAsyncCollectorVerifyDatabaseSupportedConnection");
                VerifyDatabaseSupported(connection, logger, CancellationToken.None).Wait();
            }
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
                    TelemetryInstance.TrackEvent(TelemetryEventName.FlushAsync);
                    await this.UpsertRowsAsync(this._rows, this._attribute, this._configuration);
                    this._rows.Clear();
                }
            }
            catch (Exception ex)
            {
                TelemetryInstance.TrackException(TelemetryErrorName.FlushAsync, ex);
                throw;
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
        private async Task UpsertRowsAsync(IList<T> rows, SqlAttribute attribute, IConfiguration configuration)
        {
            this._logger.LogDebugWithThreadId("BEGIN UpsertRowsAsync");
            var upsertRowsAsyncSw = Stopwatch.StartNew();
            using (SqlConnection connection = BuildConnection(attribute.ConnectionStringSetting, configuration))
            {
                this._logger.LogDebugWithThreadId("BEGIN OpenUpsertRowsAsyncConnection");
                await connection.OpenAsync();
                this._logger.LogDebugWithThreadId("END OpenUpsertRowsAsyncConnection");
                this._serverProperties = await GetServerTelemetryProperties(connection, this._logger, CancellationToken.None);
                Dictionary<TelemetryPropertyName, string> props = connection.AsConnectionProps(this._serverProperties);

                string fullTableName = attribute.CommandText;

                // Include the connection string hash as part of the key in case this customer has the same table in two different Sql Servers
                string cacheKey = $"{connection.ConnectionString.GetHashCode()}-{fullTableName}";

                ObjectCache cachedTables = MemoryCache.Default;
                var tableInfo = cachedTables[cacheKey] as TableInformation;

                int timeout = AZ_FUNC_TABLE_INFO_CACHE_TIMEOUT_MINUTES;
                string timeoutEnvVar = Environment.GetEnvironmentVariable("AZ_FUNC_TABLE_INFO_CACHE_TIMEOUT_MINUTES");
                if (!string.IsNullOrEmpty(timeoutEnvVar))
                {
                    if (int.TryParse(timeoutEnvVar, NumberStyles.Integer, CultureInfo.InvariantCulture, out timeout))
                    {
                        this._logger.LogDebugWithThreadId($"Overriding default table info cache timeout with new value {timeout}");
                    }
                    else
                    {
                        timeout = AZ_FUNC_TABLE_INFO_CACHE_TIMEOUT_MINUTES;
                    }
                }

                if (tableInfo == null)
                {
                    TelemetryInstance.TrackEvent(TelemetryEventName.TableInfoCacheMiss, props);
                    // set the columnNames for supporting T as JObject since it doesn't have columns in the member info.
                    tableInfo = await TableInformation.RetrieveTableInformationAsync(connection, fullTableName, this._logger, GetColumnNamesFromItem(rows.First()), this._serverProperties);
                    var policy = new CacheItemPolicy
                    {
                        // Re-look up the primary key(s) after timeout (default timeout is 10 minutes)
                        AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(timeout)
                    };

                    cachedTables.Set(cacheKey, tableInfo, policy);
                }
                else
                {
                    TelemetryInstance.TrackEvent(TelemetryEventName.TableInfoCacheHit, props);
                }

                IEnumerable<string> extraProperties = GetExtraProperties(tableInfo.Columns, rows.First());
                if (extraProperties.Any())
                {
                    string message = $"The following properties in {typeof(T)} do not exist in the table {fullTableName}: {string.Join(", ", extraProperties.ToArray())}.";
                    var ex = new InvalidOperationException(message);
                    TelemetryInstance.TrackException(TelemetryErrorName.PropsNotExistOnTable, ex, props);
                    throw ex;
                }

                IEnumerable<string> columnNamesFromItem = GetColumnNamesFromItem(rows.First());
                IEnumerable<string> unsupportedColumns = columnNamesFromItem.Where(prop => UnsupportedTypes.Contains(tableInfo.Columns[prop], StringComparer.OrdinalIgnoreCase));
                if (unsupportedColumns.Any())
                {
                    string message = $"The type(s) of the following column(s) are not supported: {string.Join(", ", unsupportedColumns.ToArray())}. See https://github.com/Azure/azure-functions-sql-extension#output-bindings for more details.";
                    throw new InvalidOperationException(message);
                }

                IEnumerable<string> bracketedColumnNamesFromItem = columnNamesFromItem
                    .Where(prop => !tableInfo.PrimaryKeys.Any(k => k.IsIdentity && string.Equals(k.Name, prop, StringComparison.Ordinal))) // Skip any identity columns, those should never be updated
                    .Select(prop => prop.AsBracketQuotedString());
                if (!bracketedColumnNamesFromItem.Any())
                {
                    string message = $"No property values found in item to upsert. If using query parameters, ensure that the casing of the parameter names and the property names match.";
                    var ex = new InvalidOperationException(message);
                    throw ex;
                }

                var table = new SqlObject(fullTableName);
                string mergeOrInsertQuery = tableInfo.QueryType == QueryType.Insert ? TableInformation.GetInsertQuery(table, bracketedColumnNamesFromItem) :
                    TableInformation.GetMergeQuery(tableInfo.PrimaryKeys, table, bracketedColumnNamesFromItem);

                this._logger.LogDebugWithThreadId("BEGIN UpsertRowsTransaction");
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
                        command.CommandText = $"{newDataQuery} {mergeOrInsertQuery};";
                        this._logger.LogDebugWithThreadId($"UpsertRowsTransactionBatch - Query={command.CommandText}");
                        par.Value = rowData;
                        await command.ExecuteNonQueryAsync();
                    }
                    transaction.Commit();
                    transactionSw.Stop();
                    upsertRowsAsyncSw.Stop();
                    var measures = new Dictionary<TelemetryMeasureName, double>()
                {
                    { TelemetryMeasureName.BatchCount, batchCount },
                    { TelemetryMeasureName.TransactionDurationMs, transactionSw.ElapsedMilliseconds },
                    { TelemetryMeasureName.CommandDurationMs, commandSw.ElapsedMilliseconds },
                    { TelemetryMeasureName.BatchSize, batchSize },
                    { TelemetryMeasureName.NumRows, rows.Count }
                };
                    TelemetryInstance.TrackEvent(TelemetryEventName.Upsert, props, measures);
                    this._logger.LogDebugWithThreadId($"END UpsertRowsTransaction Duration={transactionSw.ElapsedMilliseconds}ms Upserted {rows.Count} row(s) into database: {connection.Database} and table: {fullTableName}.");
                    this._logger.LogDebugWithThreadId($"END UpsertRowsAsync Duration={upsertRowsAsyncSw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    try
                    {
                        TelemetryInstance.TrackException(TelemetryErrorName.Upsert, ex, props);
                        transaction.Rollback();
                    }
                    catch (Exception ex2)
                    {
                        TelemetryInstance.TrackException(TelemetryErrorName.UpsertRollback, ex2, props);
                        string message2 = $"Encountered exception during upsert and rollback.";
                        throw new AggregateException(message2, new List<Exception> { ex, ex2 });
                    }
                    throw new InvalidOperationException($"Unexpected error upserting rows", ex);
                }
            }
        }

        /// <summary>
        /// Checks if any properties in T do not exist as columns in the table
        /// to upsert to and returns the extra property names in a List.
        /// </summary>
        /// <param name="columns"> The columns of the table to upsert to </param>
        /// <param name="rowItem"> Sample row used to get the column names when item is a JObject </param>
        /// <returns>List of property names that don't exist in the table</returns>
        private static IEnumerable<string> GetExtraProperties(IDictionary<string, string> columns, T rowItem)
        {
            if (typeof(T) == typeof(JObject))
            {
                Dictionary<string, string> dictObj = (rowItem as JObject).ToObject<Dictionary<string, string>>();
                return dictObj.Keys.Where(prop => !columns.ContainsKey(prop))
                .Select(prop => prop);
            }
            return typeof(T).GetProperties().ToList()
                .Where(prop => !columns.ContainsKey(prop.Name))
                .Select(prop => prop.Name);
        }
        /// <summary>
        /// Gets the column names from PropertyInfo when T is POCO
        /// and when T is JObject, parses the data to get column names
        /// </summary>
        /// <param name="row"> Sample row used to get the column names when item is a JObject </param>
        /// <returns>List of column names in the table</returns>
        private static IEnumerable<string> GetColumnNamesFromItem(T row)
        {
            if (typeof(T) == typeof(JObject))
            {
                var jsonObj = JObject.Parse(row.ToString());
                Dictionary<string, string> dictObj = jsonObj.ToObject<Dictionary<string, string>>();
                return dictObj.Keys;
            }
            return typeof(T).GetProperties().Select(prop => prop.Name);
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

            var uniqueUpdatedPrimaryKeys = new HashSet<string>();

            // If there are duplicate primary keys, we'll need to pick the LAST (most recent) row per primary key.
            foreach (T row in rows.Reverse())
            {
                if (typeof(T) != typeof(JObject))
                {
                    if (table.HasIdentityColumnPrimaryKeys)
                    {
                        // If the table has an identity column as a primary key then
                        // all rows are guaranteed to be unique so we can insert them all
                        rowsToUpsert.Add(row);
                    }
                    else
                    {
                        // SQL Server allows 900 bytes per primary key, so use that as a baseline
                        var combinedPrimaryKey = new StringBuilder(900 * table.PrimaryKeyProperties.Count());
                        // Look up primary key of T. Because we're going in the same order of properties every time,
                        // we can assume that if two rows with the same primary key are in the list, they will collide
                        foreach (PropertyInfo primaryKeyProperty in table.PrimaryKeyProperties)
                        {
                            object value = primaryKeyProperty.GetValue(row);
                            // Identity columns are allowed to be optional, so just skip the key if it doesn't exist
                            if (value == null)
                            {
                                continue;
                            }
                            combinedPrimaryKey.Append(value.ToString());
                        }
                        string combinedPrimaryKeyStr = combinedPrimaryKey.ToString();
                        // If we have already seen this unique primary key, skip this update
                        // If the combined key is empty that means
                        if (uniqueUpdatedPrimaryKeys.Add(combinedPrimaryKeyStr))
                        {
                            // This is the first time we've seen this particular PK. Add this row to the upsert query.
                            rowsToUpsert.Add(row);
                        }
                    }
                }
                else
                {
                    // ToDo: add check for duplicate primary keys once we find a way to get primary keys.
                    rowsToUpsert.Add(row);
                }
            }

            rowData = JsonConvert.SerializeObject(rowsToUpsert, table.JsonSerializerSettings);
            IEnumerable<string> columnNamesFromItem = GetColumnNamesFromItem(rows.First());
            IEnumerable<string> bracketColumnDefinitionsFromItem = table.Columns.Where(c => columnNamesFromItem.Contains(c.Key))
                .Select(c => $"{c.Key.AsBracketQuotedString()} {c.Value}");
            newDataQuery = $"WITH {CteName} AS ( SELECT * FROM OPENJSON({RowDataParameter}) WITH ({string.Join(",", bracketColumnDefinitionsFromItem)}) )";
        }

        public class TableInformation
        {
            public List<PrimaryKey> PrimaryKeys { get; }

            public IEnumerable<PropertyInfo> PrimaryKeyProperties { get; }

            /// <summary>
            /// All of the columns, along with their data types, for SQL to use to turn JSON into a table
            /// </summary>
            public IDictionary<string, string> Columns { get; }

            /// <summary>
            /// List of strings containing each column and its type. ex: ["Cost int", "LastChangeDate datetime(7)"]
            /// </summary>
            public IEnumerable<string> ColumnDefinitions => this.Columns.Select(c => $"{c.Key} {c.Value}");

            /// <summary>
            /// Whether to use an insert query or merge query.
            /// </summary>
            public QueryType QueryType { get; }

            /// <summary>
            /// Whether at least one of the primary keys on this table is an identity column
            /// </summary>
            public bool HasIdentityColumnPrimaryKeys { get; }
            /// <summary>
            /// Settings to use when serializing the POCO into SQL.
            /// Only serialize properties and fields that correspond to SQL columns.
            /// </summary>
            public JsonSerializerSettings JsonSerializerSettings { get; }

            public TableInformation(List<PrimaryKey> primaryKeys, IEnumerable<PropertyInfo> primaryKeyProperties, IDictionary<string, string> columns, QueryType queryType, bool hasIdentityColumnPrimaryKeys)
            {
                this.PrimaryKeys = primaryKeys;
                this.PrimaryKeyProperties = primaryKeyProperties;
                this.Columns = columns;
                this.QueryType = queryType;
                this.HasIdentityColumnPrimaryKeys = hasIdentityColumnPrimaryKeys;

                // Convert datetime strings to ISO 8061 format to avoid potential errors on the server when converting into a datetime. This
                // is the only format that are an international standard.
                // https://docs.microsoft.com/previous-versions/sql/sql-server-2008-r2/ms180878(v=sql.105)
                this.JsonSerializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new DynamicPOCOContractResolver(columns),
                    DateFormatString = ISO_8061_DATETIME_FORMAT
                };
            }

            /// <summary>
            /// Generates SQL query that can be used to retrieve the Primary Keys of a table
            /// </summary>
            public static string GetPrimaryKeysQuery(SqlObject table)
            {
                return $@"
                    SELECT
                        ccu.{ColumnName},
                        c.is_identity,
                        case
                            when isc.COLUMN_DEFAULT = NULL then 'false'
                            else 'true'
                        end as {HasDefault}
                    FROM
                        INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    INNER JOIN
                        INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE ccu ON ccu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME AND ccu.TABLE_NAME = tc.TABLE_NAME
                    INNER JOIN
                        sys.columns c ON c.object_id = OBJECT_ID({table.QuotedFullName}) AND c.name = ccu.COLUMN_NAME
                    INNER JOIN
                        INFORMATION_SCHEMA.COLUMNS isc ON isc.TABLE_NAME = {table.QuotedName} AND isc.COLUMN_NAME = ccu.COLUMN_NAME
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

            public static string GetInsertQuery(SqlObject table, IEnumerable<string> bracketedColumnNamesFromItem)
            {
                return $"INSERT INTO {table.BracketQuotedFullName} ({string.Join(",", bracketedColumnNamesFromItem)}) SELECT * FROM {CteName}";
            }

            /// <summary>
            /// Generates reusable SQL query that will be part of every upsert command.
            /// </summary>
            public static string GetMergeQuery(IList<PrimaryKey> primaryKeys, SqlObject table, IEnumerable<string> bracketedColumnNamesFromItem)
            {
                IList<string> bracketedPrimaryKeys = primaryKeys.Select(p => p.Name.AsBracketQuotedString()).ToList();
                // Generate the ON part of the merge query (compares new data against existing data)
                var primaryKeyMatchingQuery = new StringBuilder($"ExistingData.{bracketedPrimaryKeys[0]} = NewData.{bracketedPrimaryKeys[0]}");
                foreach (string primaryKey in bracketedPrimaryKeys.Skip(1))
                {
                    primaryKeyMatchingQuery.Append($" AND ExistingData.{primaryKey} = NewData.{primaryKey}");
                }

                // Generate the UPDATE part of the merge query (all columns that should be updated)
                var columnMatchingQueryBuilder = new StringBuilder();
                foreach (string column in bracketedColumnNamesFromItem)
                {
                    columnMatchingQueryBuilder.Append($" ExistingData.{column} = NewData.{column},");
                }

                string columnMatchingQuery = columnMatchingQueryBuilder.ToString().TrimEnd(',');
                return $@"
                    MERGE INTO {table.BracketQuotedFullName} WITH (HOLDLOCK)
                        AS ExistingData
                    USING {CteName}
                        AS NewData
                    ON
                        {primaryKeyMatchingQuery}
                    WHEN MATCHED THEN
                        UPDATE SET {columnMatchingQuery}
                    WHEN NOT MATCHED THEN
                        INSERT ({string.Join(",", bracketedColumnNamesFromItem)}) VALUES ({string.Join(",", bracketedColumnNamesFromItem)})";
            }

            /// <summary>
            /// Retrieve (relatively) static information of SQL Table like primary keys, column names, etc.
            /// in order to generate the MERGE portion of the upsert query.
            /// This only needs to be generated once and can be reused for subsequent upserts.
            /// </summary>
            /// <param name="sqlConnection">An open connection with which to query SQL against</param>
            /// <param name="fullName">Full name of table, including schema (if exists).</param>
            /// <param name="logger">ILogger used to log any errors or warnings.</param>
            /// <param name="objectColumnNames">Column names from the object</param>
            /// <returns>TableInformation object containing primary keys, column types, etc.</returns>
            public static async Task<TableInformation> RetrieveTableInformationAsync(SqlConnection sqlConnection, string fullName, ILogger logger, IEnumerable<string> objectColumnNames, ServerProperties serverProperties)
            {
                Dictionary<TelemetryPropertyName, string> sqlConnProps = sqlConnection.AsConnectionProps(serverProperties);
                logger.LogDebugWithThreadId("BEGIN RetrieveTableInformationAsync");
                var table = new SqlObject(fullName);

                var tableInfoSw = Stopwatch.StartNew();

                // Get all column names and types
                var columnDefinitionsFromSQL = new Dictionary<string, string>();
                var columnDefinitionsSw = Stopwatch.StartNew();
                try
                {
                    string getColumnDefinitionsQuery = GetColumnDefinitionsQuery(table);
                    logger.LogDebugWithThreadId($"BEGIN GetColumnDefinitions Query=\"{getColumnDefinitionsQuery}\"");
                    var cmdColDef = new SqlCommand(getColumnDefinitionsQuery, sqlConnection);
                    using (SqlDataReader rdr = await cmdColDef.ExecuteReaderAsync())
                    {
                        while (await rdr.ReadAsync())
                        {
                            string columnName = rdr[ColumnName].ToString();
                            columnDefinitionsFromSQL.Add(columnName, rdr[ColumnDefinition].ToString());
                        }
                        columnDefinitionsSw.Stop();
                        TelemetryInstance.TrackDuration(TelemetryEventName.GetColumnDefinitions, columnDefinitionsSw.ElapsedMilliseconds, sqlConnProps);
                        logger.LogDebugWithThreadId($"END GetColumnDefinitions Duration={columnDefinitionsSw.ElapsedMilliseconds}ms");
                    }

                }
                catch (Exception ex)
                {
                    TelemetryInstance.TrackException(TelemetryErrorName.GetColumnDefinitions, ex, sqlConnProps);
                    // Throw a custom error so that it's easier to decipher.
                    string message = $"Encountered exception while retrieving column names and types for table {table}. Cannot generate upsert command without them.";
                    throw new InvalidOperationException(message, ex);
                }

                if (columnDefinitionsFromSQL.Count == 0)
                {
                    string message = $"Table {table} does not exist.";
                    var ex = new InvalidOperationException(message);
                    TelemetryInstance.TrackException(TelemetryErrorName.GetColumnDefinitionsTableDoesNotExist, ex, sqlConnProps);
                    throw ex;
                }

                // Query SQL for table Primary Keys
                var primaryKeys = new List<PrimaryKey>();
                var primaryKeysSw = Stopwatch.StartNew();
                try
                {
                    string getPrimaryKeysQuery = GetPrimaryKeysQuery(table);
                    logger.LogDebugWithThreadId($"BEGIN GetPrimaryKeys Query=\"{getPrimaryKeysQuery}\"");
                    var cmd = new SqlCommand(getPrimaryKeysQuery, sqlConnection);
                    using (SqlDataReader rdr = await cmd.ExecuteReaderAsync())
                    {
                        while (await rdr.ReadAsync())
                        {
                            string columnName = rdr[ColumnName].ToString();
                            primaryKeys.Add(new PrimaryKey(columnName, bool.Parse(rdr[IsIdentity].ToString()), bool.Parse(rdr[HasDefault].ToString())));
                        }
                        primaryKeysSw.Stop();
                        TelemetryInstance.TrackDuration(TelemetryEventName.GetPrimaryKeys, primaryKeysSw.ElapsedMilliseconds, sqlConnProps);
                        logger.LogDebugWithThreadId($"END GetPrimaryKeys Duration={primaryKeysSw.ElapsedMilliseconds}ms");
                    }
                }
                catch (Exception ex)
                {
                    TelemetryInstance.TrackException(TelemetryErrorName.GetPrimaryKeys, ex, sqlConnProps);
                    // Throw a custom error so that it's easier to decipher.
                    string message = $"Encountered exception while retrieving primary keys for table {table}. Cannot generate upsert command without them.";
                    throw new InvalidOperationException(message, ex);
                }

                if (!primaryKeys.Any())
                {
                    string message = $"Did not retrieve any primary keys for {table}. Cannot generate upsert command without them.";
                    var ex = new InvalidOperationException(message);
                    TelemetryInstance.TrackException(TelemetryErrorName.NoPrimaryKeys, ex, sqlConnProps);
                    throw ex;
                }

                // Match SQL Primary Key column names to POCO property objects. Ensure none are missing.
                IEnumerable<PropertyInfo> primaryKeyProperties = typeof(T).GetProperties().Where(f => primaryKeys.Any(k => string.Equals(k.Name, f.Name, StringComparison.Ordinal)));
                IEnumerable<string> primaryKeysFromObject = objectColumnNames.Where(f => primaryKeys.Any(k => string.Equals(k.Name, f, StringComparison.Ordinal)));
                IEnumerable<PrimaryKey> missingPrimaryKeysFromItem = primaryKeys
                    .Where(k => !primaryKeysFromObject.Contains(k.Name));
                bool hasIdentityColumnPrimaryKeys = primaryKeys.Any(k => k.IsIdentity);
                bool hasDefaultColumnPrimaryKeys = primaryKeys.Any(k => k.HasDefault);
                // If none of the primary keys are an identity column or have a default value then we require that all primary keys be present in the POCO so we can
                // generate the MERGE statement correctly
                if (!hasIdentityColumnPrimaryKeys && !hasDefaultColumnPrimaryKeys && missingPrimaryKeysFromItem.Any())
                {
                    string message = $"All primary keys for SQL table {table} need to be found in '{typeof(T)}.' Missing primary keys: [{string.Join(",", missingPrimaryKeysFromItem)}]";
                    var ex = new InvalidOperationException(message);
                    TelemetryInstance.TrackException(TelemetryErrorName.MissingPrimaryKeys, ex, sqlConnProps);
                    throw ex;
                }

                // If any identity columns or columns with default values aren't included in the object then we have to generate a basic insert since the merge statement expects all primary key
                // columns to exist. (the merge statement can handle nullable columns though if those exist)
                QueryType queryType = (hasIdentityColumnPrimaryKeys || hasDefaultColumnPrimaryKeys) && missingPrimaryKeysFromItem.Any() ? QueryType.Insert : QueryType.Merge;

                tableInfoSw.Stop();
                var durations = new Dictionary<TelemetryMeasureName, double>()
                {
                    { TelemetryMeasureName.GetColumnDefinitionsDurationMs, columnDefinitionsSw.ElapsedMilliseconds },
                    { TelemetryMeasureName.GetPrimaryKeysDurationMs, primaryKeysSw.ElapsedMilliseconds }
                };
                sqlConnProps.Add(TelemetryPropertyName.QueryType, queryType.ToString());
                sqlConnProps.Add(TelemetryPropertyName.HasIdentityColumn, hasIdentityColumnPrimaryKeys.ToString());
                TelemetryInstance.TrackDuration(TelemetryEventName.GetTableInfo, tableInfoSw.ElapsedMilliseconds, sqlConnProps, durations);
                logger.LogDebugWithThreadId($"END RetrieveTableInformationAsync Duration={tableInfoSw.ElapsedMilliseconds}ms DB and Table: {sqlConnection.Database}.{fullName}. Primary keys: [{string.Join(",", primaryKeys.Select(pk => pk.Name))}]. SQL Column and Definitions:  [{string.Join(",", columnDefinitionsFromSQL)}] Object columns: [{string.Join(",", objectColumnNames)}]");
                return new TableInformation(primaryKeys, primaryKeyProperties, columnDefinitionsFromSQL, queryType, hasIdentityColumnPrimaryKeys);
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
                    .ToDictionary(p => p.PropertyName);

                // Make sure the ordering of columns matches that of SQL
                // Necessary for proper matching of column names to JSON that is generated for each batch of data
                IList<JsonProperty> propertiesToSerialize = new List<JsonProperty>(properties.Count);
                foreach (KeyValuePair<string, string> column in this._propertiesToSerialize)
                {
                    if (properties.TryGetValue(column.Key, out JsonProperty value))
                    {
                        JsonProperty sqlColumn = value;
                        sqlColumn.PropertyName = sqlColumn.PropertyName;
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