// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Represents the listener to SQL table changes.
    /// </summary>
    /// <typeparam name="T">POCO class representing the row in the user table</typeparam>
    internal sealed class SqlTriggerListener<T> : IListener
    {
        private const int ListenerNotStarted = 0;
        private const int ListenerStarting = 1;
        private const int ListenerStarted = 2;
        private const int ListenerStopping = 3;
        private const int ListenerStopped = 4;

        private readonly SqlObject _userTable;
        private readonly string _connectionString;
        private readonly string _userFunctionId;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ILogger _logger;

        private SqlTableChangeMonitor<T> _changeMonitor;
        private int _listenerState;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlTriggerListener{T}"/> class.
        /// </summary>
        /// <param name="connectionString">SQL connection string used to connect to user database</param>
        /// <param name="tableName">Name of the user table</param>
        /// <param name="userFunctionId">Unique identifier for the user function</param>
        /// <param name="executor">Defines contract for triggering user function</param>
        /// <param name="logger">Facilitates logging of messages</param>
        public SqlTriggerListener(string connectionString, string tableName, string userFunctionId, ITriggeredFunctionExecutor executor, ILogger logger)
        {
            _ = !string.IsNullOrEmpty(connectionString) ? true : throw new ArgumentNullException(nameof(connectionString));
            _ = !string.IsNullOrEmpty(tableName) ? true : throw new ArgumentNullException(nameof(tableName));
            _ = !string.IsNullOrEmpty(userFunctionId) ? true : throw new ArgumentNullException(nameof(userFunctionId));
            _ = executor ?? throw new ArgumentNullException(nameof(executor));
            _ = logger ?? throw new ArgumentNullException(nameof(logger));

            this._connectionString = connectionString;
            this._userTable = new SqlObject(tableName);
            this._userFunctionId = userFunctionId;
            this._executor = executor;
            this._logger = logger;
            this._listenerState = ListenerNotStarted;
        }

        public void Cancel()
        {
            this.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            // Nothing to dispose.
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            int previousState = Interlocked.CompareExchange(ref this._listenerState, ListenerStarting, ListenerNotStarted);

            switch (previousState)
            {
                case ListenerStarting: throw new InvalidOperationException("The listener is already starting.");
                case ListenerStarted: throw new InvalidOperationException("The listener has already started.");
                default: break;
            }

            try
            {
                using (var connection = new SqlConnection(this._connectionString))
                {
                    await connection.OpenAsync(cancellationToken);

                    int userTableId = await this.GetUserTableIdAsync(connection, cancellationToken);
                    IReadOnlyList<(string name, string type)> primaryKeyColumns = await this.GetPrimaryKeyColumnsAsync(connection, userTableId, cancellationToken);
                    IReadOnlyList<string> userTableColumns = await this.GetUserTableColumnsAsync(connection, userTableId, cancellationToken);

                    string workerTableName = string.Format(CultureInfo.InvariantCulture, SqlTriggerConstants.WorkerTableNameFormat, $"{this._userFunctionId}_{userTableId}");
                    this._logger.LogDebug($"Worker table name: '{workerTableName}'.");

                    using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
                    {
                        await CreateSchemaAsync(connection, transaction, cancellationToken);
                        await CreateGlobalStateTableAsync(connection, transaction, cancellationToken);
                        await this.InsertGlobalStateTableRowAsync(connection, transaction, userTableId, cancellationToken);
                        await CreateWorkerTableAsync(connection, transaction, workerTableName, primaryKeyColumns, cancellationToken);
                        transaction.Commit();
                    }

                    this._logger.LogInformation($"Starting SQL trigger listener for table: '{this._userTable.FullName}', function ID: '{this._userFunctionId}'.");

                    // TODO: Check if passing the cancellation token would be beneficial.
                    this._changeMonitor = new SqlTableChangeMonitor<T>(
                        this._connectionString,
                        userTableId,
                        this._userTable,
                        this._userFunctionId,
                        workerTableName,
                        userTableColumns,
                        primaryKeyColumns.Select(col => col.name).ToList(),
                        this._executor,
                        this._logger);

                    this._listenerState = ListenerStarted;
                    this._logger.LogInformation($"Started SQL trigger listener for table: '{this._userTable.FullName}', function ID: '{this._userFunctionId}'.");
                }
            }
            catch (Exception ex)
            {
                this._listenerState = ListenerNotStarted;
                this._logger.LogError($"Failed to start SQL trigger listener for table: '{this._userTable.FullName}', function ID: '{this._userFunctionId}'. Exception: {ex}");

                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            int previousState = Interlocked.CompareExchange(ref this._listenerState, ListenerStopping, ListenerStarted);
            if (previousState == ListenerStarted)
            {
                this._changeMonitor.Dispose();

                this._listenerState = ListenerStopped;
                this._logger.LogInformation($"Stopped SQL trigger listener for table: '{this._userTable.FullName}', function ID: '{this._userFunctionId}'.");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Returns the object ID of the user table.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown in case of error when querying the object ID for the user table</exception>
        private async Task<int> GetUserTableIdAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            string getObjectIdQuery = $"SELECT OBJECT_ID(N{this._userTable.QuotedFullName}, 'U');";

            using (var getObjectIdCommand = new SqlCommand(getObjectIdQuery, connection))
            using (SqlDataReader reader = await getObjectIdCommand.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException($"Received empty response when querying the object ID for table: '{this._userTable.FullName}'.");
                }

                object userTableId = reader.GetValue(0);

                if (userTableId is DBNull)
                {
                    throw new InvalidOperationException($"Could not find table: '{this._userTable.FullName}'.");
                }

                return (int)userTableId;
            }
        }

        /// <summary>
        /// Gets the names and types of primary key columns of the user table.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if there are no primary key columns present in the user table or if their names conflict with columns in worker table.
        /// </exception>
        private async Task<IReadOnlyList<(string name, string type)>> GetPrimaryKeyColumnsAsync(SqlConnection connection, int userTableId, CancellationToken cancellationToken)
        {
            string getPrimaryKeyColumnsQuery = $@"
                SELECT c.name, t.name, c.max_length, c.precision, c.scale
                FROM sys.indexes AS i
                INNER JOIN sys.index_columns AS ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns AS c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                INNER JOIN sys.types AS t ON c.user_type_id = t.user_type_id
                WHERE i.is_primary_key = 1 AND i.object_id = {userTableId};
            ";

            using (var getPrimaryKeyColumnsCommand = new SqlCommand(getPrimaryKeyColumnsQuery, connection))
            using (SqlDataReader reader = await getPrimaryKeyColumnsCommand.ExecuteReaderAsync(cancellationToken))
            {
                string[] variableLengthTypes = new[] { "varchar", "nvarchar", "nchar", "char", "binary", "varbinary" };
                string[] variablePrecisionTypes = new[] { "numeric", "decimal" };

                var primaryKeyColumns = new List<(string name, string type)>();

                while (await reader.ReadAsync(cancellationToken))
                {
                    string name = reader.GetString(0);
                    string type = reader.GetString(1);

                    if (variableLengthTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                    {
                        // Special "max" case. I'm actually not sure it's valid to have varchar(max) as a primary key because
                        // it exceeds the byte limit of an index field (900 bytes), but just in case
                        short length = reader.GetInt16(2);
                        type += length == -1 ? "(max)" : $"({length})";
                    }
                    else if (variablePrecisionTypes.Contains(type))
                    {
                        byte precision = reader.GetByte(3);
                        byte scale = reader.GetByte(4);
                        type += $"({precision},{scale})";
                    }

                    primaryKeyColumns.Add((name, type));
                }

                if (primaryKeyColumns.Count == 0)
                {
                    throw new InvalidOperationException($"Could not find primary key created in table: '{this._userTable.FullName}'.");
                }

                string[] reservedColumnNames = new[] { "ChangeVersion", "AttemptCount", "LeaseExpirationTime" };
                var conflictingColumnNames = primaryKeyColumns.Select(col => col.name).Intersect(reservedColumnNames).ToList();

                if (conflictingColumnNames.Count > 0)
                {
                    throw new InvalidOperationException($"Found reserved column name(s): '{string.Join(", ", conflictingColumnNames)}' in table: '{this._userTable.FullName}'." +
                        " Please rename them to be able to use trigger binding.");
                }

                this._logger.LogDebug($"Primary key column names(types): {string.Join(", ", primaryKeyColumns.Select(col => $"'{col.name}({col.type})'"))}.");
                return primaryKeyColumns;
            }
        }

        /// <summary>
        /// Gets the column names of the user table.
        /// </summary>
        private async Task<IReadOnlyList<string>> GetUserTableColumnsAsync(SqlConnection connection, int userTableId, CancellationToken cancellationToken)
        {
            string getUserTableColumnsQuery = $"SELECT name FROM sys.columns WHERE object_id = {userTableId};";

            using (var getUserTableColumnsCommand = new SqlCommand(getUserTableColumnsQuery, connection))
            using (SqlDataReader reader = await getUserTableColumnsCommand.ExecuteReaderAsync(cancellationToken))
            {
                var userTableColumns = new List<string>();

                while (await reader.ReadAsync(cancellationToken))
                {
                    userTableColumns.Add(reader.GetString(0));
                }

                this._logger.LogDebug($"User table column names: {string.Join(", ", userTableColumns.Select(col => $"'{col}'"))}.");
                return userTableColumns;
            }
        }

        /// <summary>
        /// Creates the schema for global state table and worker tables, if it does not already exist.
        /// </summary>
        private static async Task CreateSchemaAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            string createSchemaQuery = $@"
                IF SCHEMA_ID(N'{SqlTriggerConstants.SchemaName}') IS NULL
                    EXEC ('CREATE SCHEMA {SqlTriggerConstants.SchemaName}');
            ";

            using (var createSchemaCommand = new SqlCommand(createSchemaQuery, connection, transaction))
            {
                await createSchemaCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Creates the global state table if it does not already exist.
        /// </summary>
        private static async Task CreateGlobalStateTableAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            string createGlobalStateTableQuery = $@"
                IF OBJECT_ID(N'{SqlTriggerConstants.GlobalStateTableName}', 'U') IS NULL
                    CREATE TABLE {SqlTriggerConstants.GlobalStateTableName} (
                        UserFunctionID char(16) NOT NULL,
                        UserTableID int NOT NULL,
                        LastSyncVersion bigint NOT NULL,
                        PRIMARY KEY (UserFunctionID, UserTableID)
                    );
            ";

            using (var createGlobalStateTableCommand = new SqlCommand(createGlobalStateTableQuery, connection, transaction))
            {
                await createGlobalStateTableCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Inserts row for the 'user function and table' inside the global state table, if one does not already exist.
        /// </summary>
        private async Task InsertGlobalStateTableRowAsync(SqlConnection connection, SqlTransaction transaction, int userTableId, CancellationToken cancellationToken)
        {
            object minValidVersion;

            string getMinValidVersionQuery = $"SELECT CHANGE_TRACKING_MIN_VALID_VERSION({userTableId});";

            using (var getMinValidVersionCommand = new SqlCommand(getMinValidVersionQuery, connection, transaction))
            using (SqlDataReader reader = await getMinValidVersionCommand.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException($"Received empty response when querying the 'change tracking min valid version' for table: '{this._userTable.FullName}'.");
                }

                minValidVersion = reader.GetValue(0);

                if (minValidVersion is DBNull)
                {
                    throw new InvalidOperationException($"Could not find change tracking enabled for table: '{this._userTable.FullName}'.");
                }
            }

            string insertRowGlobalStateTableQuery = $@"
                IF NOT EXISTS (
                    SELECT * FROM {SqlTriggerConstants.GlobalStateTableName}
                    WHERE UserFunctionID = '{this._userFunctionId}' AND UserTableID = {userTableId}
                )
                    INSERT INTO {SqlTriggerConstants.GlobalStateTableName}
                    VALUES ('{this._userFunctionId}', {userTableId}, {(long)minValidVersion});
            ";

            using (var insertRowGlobalStateTableCommand = new SqlCommand(insertRowGlobalStateTableQuery, connection, transaction))
            {
                await insertRowGlobalStateTableCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Creates the worker table for the 'user function and table', if one does not already exist.
        /// </summary>
        private static async Task CreateWorkerTableAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string workerTableName,
            IReadOnlyList<(string name, string type)> primaryKeyColumns,
            CancellationToken cancellationToken)
        {
            string primaryKeysWithTypes = string.Join(", ", primaryKeyColumns.Select(col => $"{col.name.AsBracketQuotedString()} {col.type}"));
            string primaryKeys = string.Join(", ", primaryKeyColumns.Select(col => col.name.AsBracketQuotedString()));

            string createWorkerTableQuery = $@"
                IF OBJECT_ID(N'{workerTableName}', 'U') IS NULL
                    CREATE TABLE {workerTableName} (
                        {primaryKeysWithTypes},
                        ChangeVersion bigint NOT NULL,
                        AttemptCount int NOT NULL,
                        LeaseExpirationTime datetime2,
                        PRIMARY KEY ({primaryKeys})
                    );
            ";

            using (var createWorkerTableCommand = new SqlCommand(createWorkerTableQuery, connection, transaction))
            {
                await createWorkerTableCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }
}