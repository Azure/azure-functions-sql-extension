// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlTriggerConstants;
using static Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry.Telemetry;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    public static class SqlTriggerUtils
    {

        /// <summary>
        /// Gets the names and types of primary key columns of the user table.
        /// </summary>
        /// <param name="connection">SQL connection used to connect to user database</param>
        /// <param name="userTableId">ID of the user table</param>
        /// <param name="logger">Facilitates logging of messages</param>
        /// <param name="userTableName">Name of the user table, doesn't need to be escaped since it's only used for logging</param>
        /// <param name="cancellationToken">Cancellation token to pass to the command</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if there are no primary key columns present in the user table or if their names conflict with columns in leases table.
        /// </exception>
        public static IReadOnlyList<(string name, string type)> GetPrimaryKeyColumns(SqlConnection connection, int userTableId, ILogger logger, string userTableName, CancellationToken cancellationToken)
        {
            const int NameIndex = 0, TypeIndex = 1, LengthIndex = 2, PrecisionIndex = 3, ScaleIndex = 4;
            string getPrimaryKeyColumnsQuery = $@"
                SELECT
                    c.name,
                    t.name,
                    c.max_length,
                    c.precision,
                    c.scale
                FROM sys.indexes AS i
                INNER JOIN sys.index_columns AS ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns AS c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                INNER JOIN sys.types AS t ON c.user_type_id = t.user_type_id
                WHERE i.is_primary_key = 1 AND i.object_id = {userTableId};
            ";
            using (var getPrimaryKeyColumnsCommand = new SqlCommand(getPrimaryKeyColumnsQuery, connection))
            using (SqlDataReader reader = getPrimaryKeyColumnsCommand.ExecuteReaderWithLogging(logger))
            {
                string[] variableLengthTypes = new[] { "varchar", "nvarchar", "nchar", "char", "binary", "varbinary" };
                string[] variablePrecisionTypes = new[] { "numeric", "decimal" };

                var primaryKeyColumns = new List<(string name, string type)>();

                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string name = reader.GetString(NameIndex);
                    string type = reader.GetString(TypeIndex);

                    if (variableLengthTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                    {
                        // Special "max" case. I'm actually not sure it's valid to have varchar(max) as a primary key because
                        // it exceeds the byte limit of an index field (900 bytes), but just in case
                        short length = reader.GetInt16(LengthIndex);
                        type += length == -1 ? "(max)" : $"({length})";
                    }
                    else if (variablePrecisionTypes.Contains(type))
                    {
                        byte precision = reader.GetByte(PrecisionIndex);
                        byte scale = reader.GetByte(ScaleIndex);
                        type += $"({precision},{scale})";
                    }

                    primaryKeyColumns.Add((name, type));
                }

                if (primaryKeyColumns.Count == 0)
                {
                    throw new InvalidOperationException($"Could not find primary key created in table: '{userTableName}'.");
                }

                logger.LogDebug($"GetPrimaryKeyColumns ColumnNames(types) = {string.Join(", ", primaryKeyColumns.Select(col => $"'{col.name}({col.type})'"))}.");
                return primaryKeyColumns;
            }
        }

        /// <summary>
        /// Returns the object ID of the user table.
        /// </summary>
        /// <param name="connection">SQL connection used to connect to user database</param>
        /// <param name="userTable">SqlObject user table</param>
        /// <param name="logger">Facilitates logging of messages</param>
        /// <param name="cancellationToken">Cancellation token to pass to the command</param>
        /// <exception cref="InvalidOperationException">Thrown in case of error when querying the object ID for the user table</exception>
        internal static async Task<int> GetUserTableIdAsync(SqlConnection connection, SqlObject userTable, ILogger logger, CancellationToken cancellationToken)
        {
            string getObjectIdQuery = $"SELECT OBJECT_ID(N{userTable.QuotedFullName}, 'U');";

            using (var getObjectIdCommand = new SqlCommand(getObjectIdQuery, connection))
            using (SqlDataReader reader = getObjectIdCommand.ExecuteReaderWithLogging(logger))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException($"Received empty response when querying the object ID for table: '{userTable.FullName}'.");
                }

                object userTableId = reader.GetValue(0);

                if (userTableId is DBNull)
                {
                    throw new InvalidOperationException($"Could not find table: '{userTable.FullName}'.");
                }
                logger.LogDebug($"GetUserTableId TableId={userTableId}");
                return (int)userTableId;
            }
        }

        /// <summary>
        /// Returns the formatted leases table name. If userDefinedLeasesTableName is null, the default name Leases_{FunctionId}_{TableId} is used.
        /// </summary>
        /// <param name="userDefinedLeasesTableName">Leases table name defined by the user</param>
        /// <param name="userTableId">SQL object ID of the user table</param>
        /// <param name="userFunctionId">Unique identifier for the user function</param>
        internal static string GetBracketedLeasesTableName(string userDefinedLeasesTableName, string userFunctionId, int userTableId)
        {
            return string.IsNullOrEmpty(userDefinedLeasesTableName) ? string.Format(CultureInfo.InvariantCulture, LeasesTableNameFormat, $"{userFunctionId}_{userTableId}") :
                string.Format(CultureInfo.InvariantCulture, UserDefinedLeasesTableNameFormat, $"{userDefinedLeasesTableName.AsBracketQuotedString()}");
        }

        /// <summary>
        /// Creates the schema for global state table and leases tables, if it does not already exist.
        /// </summary>
        /// <param name="connection">The already-opened connection to use for executing the command</param>
        /// <param name="transaction">The transaction wrapping this command</param>
        /// <param name="telemetryProps">The property bag for telemetry</param>
        /// <param name="logger">Facilitates logging of messages</param>
        /// <param name="cancellationToken">Cancellation token to pass to the command</param>
        /// <returns>The time taken in ms to execute the command</returns>
        internal static async Task<long> CreateSchemaAsync(SqlConnection connection, SqlTransaction transaction, IDictionary<TelemetryPropertyName, string> telemetryProps, ILogger logger, CancellationToken cancellationToken)
        {
            string createSchemaQuery = $@"
                {AppLockStatements}

                IF SCHEMA_ID(N'{SchemaName}') IS NULL
                    EXEC ('CREATE SCHEMA {SchemaName}');
            ";

            using (var createSchemaCommand = new SqlCommand(createSchemaQuery, connection, transaction))
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    await createSchemaCommand.ExecuteNonQueryAsyncWithLogging(logger, cancellationToken);
                }
                catch (Exception ex)
                {
                    TelemetryInstance.TrackException(TelemetryErrorName.CreateSchema, ex, telemetryProps);
                    var sqlEx = ex as SqlException;
                    if (sqlEx?.Number == ObjectAlreadyExistsErrorNumber)
                    {
                        // This generally shouldn't happen since we check for its existence in the statement but occasionally
                        // a race condition can make it so that multiple instances will try and create the schema at once.
                        // In that case we can just ignore the error since all we care about is that the schema exists at all.
                        logger.LogWarning($"Failed to create schema '{SchemaName}'. Exception message: {ex.Message} This is informational only, function startup will continue as normal.");
                    }
                    else
                    {
                        throw;
                    }
                }

                return stopwatch.ElapsedMilliseconds;
            }
        }

        /// <summary>
        /// Creates the global state table if it does not already exist.
        /// </summary>
        /// <param name="connection">The already-opened connection to use for executing the command</param>
        /// <param name="transaction">The transaction wrapping this command</param>
        /// <param name="telemetryProps">The property bag for telemetry</param>
        /// <param name="logger">Facilitates logging of messages</param>
        /// <param name="cancellationToken">Cancellation token to pass to the command</param>
        /// <returns>The time taken in ms to execute the command</returns>
        internal static async Task<long> CreateGlobalStateTableAsync(SqlConnection connection, SqlTransaction transaction, IDictionary<TelemetryPropertyName, string> telemetryProps, ILogger logger, CancellationToken cancellationToken)
        {
            string createGlobalStateTableQuery = $@"
                {AppLockStatements}

                IF OBJECT_ID(N'{GlobalStateTableName}', 'U') IS NULL
                    CREATE TABLE {GlobalStateTableName} (
                        UserFunctionID char(16) NOT NULL,
                        UserTableID int NOT NULL,
                        LastSyncVersion bigint NOT NULL,
                        LastAccessTime Datetime NOT NULL DEFAULT GETUTCDATE(),
                        PRIMARY KEY (UserFunctionID, UserTableID)
                    );
                ELSE IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE Name = N'LastAccessTime'
                    AND Object_ID = Object_ID(N'{GlobalStateTableName}'))
                        ALTER TABLE {GlobalStateTableName} ADD LastAccessTime Datetime NOT NULL DEFAULT GETUTCDATE();
            ";

            using (var createGlobalStateTableCommand = new SqlCommand(createGlobalStateTableQuery, connection, transaction))
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    await createGlobalStateTableCommand.ExecuteNonQueryAsyncWithLogging(logger, cancellationToken);
                }
                catch (Exception ex)
                {
                    TelemetryInstance.TrackException(TelemetryErrorName.CreateGlobalStateTable, ex, telemetryProps);
                    var sqlEx = ex as SqlException;
                    if (sqlEx?.Number == ObjectAlreadyExistsErrorNumber)
                    {
                        // This generally shouldn't happen since we check for its existence in the statement but occasionally
                        // a race condition can make it so that multiple instances will try and create the schema at once.
                        // In that case we can just ignore the error since all we care about is that the schema exists at all.
                        logger.LogWarning($"Failed to create global state table '{GlobalStateTableName}'. Exception message: {ex.Message} This is informational only, function startup will continue as normal.");
                    }
                    else
                    {
                        throw;
                    }
                }
                return stopwatch.ElapsedMilliseconds;
            }
        }

        /// <summary>
        /// Inserts row for the 'user function and table' inside the global state table, if one does not already exist.
        /// </summary>
        /// <param name="connection">The already-opened connection to use for executing the command</param>
        /// <param name="transaction">The transaction wrapping this command</param>
        /// <param name="userTableId">The ID of the table being watched</param>
        /// <param name="userTable">The User table being watched for Trigger function</param>
        /// <param name="oldUserFunctionId">deprecated user function id value created using hostId for the user function</param>
        /// <param name="userFunctionId">Unique identifier for the user function</param>
        /// <param name="logger">Facilitates logging of messages</param>
        /// <param name="cancellationToken">Cancellation token to pass to the command</param>
        /// <returns>The time taken in ms to execute the command</returns>
        internal static async Task<long> InsertGlobalStateTableRowAsync(SqlConnection connection, SqlTransaction transaction, int userTableId, SqlObject userTable, string oldUserFunctionId, string userFunctionId, ILogger logger, CancellationToken cancellationToken)
        {
            object minValidVersion;
            string getMinValidVersionQuery = $"SELECT CHANGE_TRACKING_MIN_VALID_VERSION({userTableId});";

            using (var getMinValidVersionCommand = new SqlCommand(getMinValidVersionQuery, connection, transaction))
            using (SqlDataReader reader = getMinValidVersionCommand.ExecuteReaderWithLogging(logger))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException($"Received empty response when querying the 'change tracking min valid version' for table: '{userTable.FullName}'.");
                }

                minValidVersion = reader.GetValue(0);

                if (minValidVersion is DBNull)
                {
                    throw new InvalidOperationException($"Could not find change tracking enabled for table: '{userTable.FullName}'.");
                }
            }

            string insertRowGlobalStateTableQuery = $@"
                {AppLockStatements}
                -- For back compatibility copy the lastSyncVersion from _oldUserFunctionId if it exists.
                IF NOT EXISTS (
                    SELECT * FROM {GlobalStateTableName}
                    WHERE UserFunctionID = '{userFunctionId}' AND UserTableID = {userTableId}
                )
                BEGIN
                    -- Migrate LastSyncVersion from oldUserFunctionId if it exists and delete the record
                    DECLARE @lastSyncVersion bigint;
                    SELECT @lastSyncVersion = LastSyncVersion from az_func.GlobalState where UserFunctionID = '{oldUserFunctionId}' AND UserTableID = {userTableId}
                    IF @lastSyncVersion IS NULL
                        SET @lastSyncVersion = {(long)minValidVersion};
                    ELSE
                        DELETE FROM az_func.GlobalState WHERE UserFunctionID = '{oldUserFunctionId}' AND UserTableID = {userTableId}
                    
                    INSERT INTO {GlobalStateTableName}
                    VALUES ('{userFunctionId}', {userTableId}, @lastSyncVersion, GETUTCDATE());
                END
            ";

            using (var insertRowGlobalStateTableCommand = new SqlCommand(insertRowGlobalStateTableQuery, connection, transaction))
            {
                var stopwatch = Stopwatch.StartNew();
                int rowsInserted = await insertRowGlobalStateTableCommand.ExecuteNonQueryAsyncWithLogging(logger, cancellationToken);
                if (rowsInserted > 0)
                {
                    TelemetryInstance.TrackEvent(TelemetryEventName.InsertGlobalStateTableRow);
                }
                return stopwatch.ElapsedMilliseconds;
            }
        }

        /// <summary>
        /// Creates the leases table for the 'user function and table', if one does not already exist.
        /// </summary>
        /// <param name="connection">The already-opened connection to use for executing the command</param>
        /// <param name="transaction">The transaction wrapping this command</param>
        /// <param name="leasesTableName">The name of the leases table to create</param>
        /// <param name="primaryKeyColumns">The primary keys of the user table this leases table is for</param>
        /// <param name="oldUserFunctionId">deprecated user function id value created using hostId for the user function</param>
        /// <param name="userFunctionId">Unique identifier for the user function</param>
        /// <param name="telemetryProps"></param>
        /// <param name="logger">Facilitates logging of messages</param>
        /// <param name="cancellationToken">Cancellation token to pass to the command</param>
        /// <returns>The time taken in ms to execute the command</returns>
        internal static async Task<long> CreateLeasesTableAsync(
            SqlConnection connection,
            SqlTransaction transaction,
            string leasesTableName,
            IReadOnlyList<(string name, string type)> primaryKeyColumns,
            string oldUserFunctionId,
            string userFunctionId,
            IDictionary<TelemetryPropertyName, string> telemetryProps,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            string primaryKeysWithTypes = string.Join(", ", primaryKeyColumns.Select(col => $"{col.name.AsBracketQuotedString()} {col.type}"));
            string primaryKeys = string.Join(", ", primaryKeyColumns.Select(col => col.name.AsBracketQuotedString()));
            string oldLeasesTableName = leasesTableName.Contains(userFunctionId) ? leasesTableName.Replace(userFunctionId, oldUserFunctionId) : string.Empty;

            string createLeasesTableQuery = string.IsNullOrEmpty(oldLeasesTableName) ? $@"
                {AppLockStatements}

                IF OBJECT_ID(N'{leasesTableName}', 'U') IS NULL
                    CREATE TABLE {leasesTableName} (
                        {primaryKeysWithTypes},
                        {LeasesTableChangeVersionColumnName} bigint NOT NULL,
                        {LeasesTableAttemptCountColumnName} int NOT NULL,
                        {LeasesTableLeaseExpirationTimeColumnName} datetime2,
                        PRIMARY KEY ({primaryKeys})
                    );
            " : $@"
                {AppLockStatements}

                IF OBJECT_ID(N'{leasesTableName}', 'U') IS NULL
                BEGIN
                    CREATE TABLE {leasesTableName} (
                        {primaryKeysWithTypes},
                        {LeasesTableChangeVersionColumnName} bigint NOT NULL,
                        {LeasesTableAttemptCountColumnName} int NOT NULL,
                        {LeasesTableLeaseExpirationTimeColumnName} datetime2,
                        PRIMARY KEY ({primaryKeys})
                    );

                    -- Migrate all data from OldLeasesTable and delete it.
                    IF OBJECT_ID(N'{oldLeasesTableName}', 'U') IS NOT NULL
                    BEGIN
                        INSERT INTO {leasesTableName}
                        SELECT * FROM {oldLeasesTableName};

                        DROP TABLE {oldLeasesTableName};
                    END
                End
            ";

            using (var createLeasesTableCommand = new SqlCommand(createLeasesTableQuery, connection, transaction))
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    await createLeasesTableCommand.ExecuteNonQueryAsyncWithLogging(logger, cancellationToken);
                }
                catch (Exception ex)
                {
                    TelemetryInstance.TrackException(TelemetryErrorName.CreateLeasesTable, ex, telemetryProps);
                    var sqlEx = ex as SqlException;
                    if (sqlEx?.Number == ObjectAlreadyExistsErrorNumber)
                    {
                        // This generally shouldn't happen since we check for its existence in the statement but occasionally
                        // a race condition can make it so that multiple instances will try and create the schema at once.
                        // In that case we can just ignore the error since all we care about is that the schema exists at all.
                        logger.LogWarning($"Failed to create leases table '{leasesTableName}'. Exception message: {ex.Message} This is informational only, function startup will continue as normal.");
                    }
                    else
                    {
                        throw;
                    }
                }
                long durationMs = stopwatch.ElapsedMilliseconds;
                return durationMs;
            }
        }
    }
}