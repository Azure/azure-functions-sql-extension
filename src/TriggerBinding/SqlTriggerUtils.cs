// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

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
        public static async Task<IReadOnlyList<(string name, string type)>> GetPrimaryKeyColumnsAsync(SqlConnection connection, int userTableId, ILogger logger, string userTableName, CancellationToken cancellationToken)
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
            using (SqlDataReader reader = await getPrimaryKeyColumnsCommand.ExecuteReaderAsyncWithLogging(logger, cancellationToken))
            {
                string[] variableLengthTypes = new[] { "varchar", "nvarchar", "nchar", "char", "binary", "varbinary" };
                string[] variablePrecisionTypes = new[] { "numeric", "decimal" };

                var primaryKeyColumns = new List<(string name, string type)>();

                while (await reader.ReadAsync(cancellationToken))
                {
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
            using (SqlDataReader reader = await getObjectIdCommand.ExecuteReaderAsyncWithLogging(logger, cancellationToken))
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
    }
}