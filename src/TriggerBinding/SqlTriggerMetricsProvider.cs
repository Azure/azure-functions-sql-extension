// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry;
using static Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry.Telemetry;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlTriggerConstants;
using static Microsoft.Azure.WebJobs.Extensions.Sql.SqlTriggerUtils;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Provider class for unprocessed changes metrics for SQL trigger scaling.
    /// </summary>
    internal class SqlTriggerMetricsProvider
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;
        private readonly SqlObject _userTable;
        private readonly string _userFunctionId;

        public SqlTriggerMetricsProvider(string connectionString, ILogger logger, SqlObject userTable, string userFunctionId)
        {
            this._connectionString = !string.IsNullOrEmpty(connectionString) ? connectionString : throw new ArgumentNullException(nameof(connectionString));
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._userTable = userTable ?? throw new ArgumentNullException(nameof(userTable));
            this._userFunctionId = !string.IsNullOrEmpty(userFunctionId) ? userFunctionId : throw new ArgumentNullException(nameof(userFunctionId));
        }
        public async Task<SqlTriggerMetrics> GetMetricsAsync()
        {
            return new SqlTriggerMetrics
            {
                UnprocessedChangeCount = await this.GetUnprocessedChangeCountAsync(),
                Timestamp = DateTime.UtcNow,
            };
        }
        private async Task<long> GetUnprocessedChangeCountAsync()
        {
            long unprocessedChangeCount = 0L;
            long getUnprocessedChangesDurationMs = 0L;

            try
            {
                using (var connection = new SqlConnection(this._connectionString))
                {
                    await connection.OpenAsync();

                    int userTableId = await GetUserTableIdAsync(connection, this._userTable, this._logger, CancellationToken.None);
                    IReadOnlyList<(string name, string type)> primaryKeyColumns = await GetPrimaryKeyColumnsAsync(connection, userTableId, this._logger, this._userTable.FullName, CancellationToken.None);

                    // Use a transaction to automatically release the app lock when we're done executing the query
                    using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead))
                    {
                        try
                        {
                            using (SqlCommand getUnprocessedChangesCommand = this.BuildGetUnprocessedChangesCommand(connection, transaction, primaryKeyColumns, userTableId))
                            {
                                var commandSw = Stopwatch.StartNew();
                                unprocessedChangeCount = (long)await getUnprocessedChangesCommand.ExecuteScalarAsyncWithLogging(this._logger, CancellationToken.None);
                                getUnprocessedChangesDurationMs = commandSw.ElapsedMilliseconds;
                            }

                            transaction.Commit();
                        }
                        catch (Exception)
                        {
                            try
                            {
                                transaction.Rollback();
                            }
                            catch (Exception ex2)
                            {
                                this._logger.LogError($"GetUnprocessedChangeCount : Failed to rollback transaction due to exception: {ex2.GetType()}. Exception message: {ex2.Message}");
                                TelemetryInstance.TrackException(TelemetryErrorName.GetUnprocessedChangeCountRollback, ex2);
                            }
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this._logger.LogError($"Failed to query count of unprocessed changes for table '{this._userTable.FullName}' due to exception: {ex.GetType()}. Exception message: {ex.Message}");
                TelemetryInstance.TrackException(TelemetryErrorName.GetUnprocessedChangeCount, ex, null, new Dictionary<TelemetryMeasureName, double>() { { TelemetryMeasureName.GetUnprocessedChangesDurationMs, getUnprocessedChangesDurationMs } });
                throw;
            }

            return unprocessedChangeCount;
        }
        private SqlCommand BuildGetUnprocessedChangesCommand(SqlConnection connection, SqlTransaction transaction, IReadOnlyList<(string name, string type)> primaryKeyColumns, int userTableId)
        {
            string leasesTableJoinCondition = string.Join(" AND ", primaryKeyColumns.Select(col => $"c.{col.name.AsBracketQuotedString()} = l.{col.name.AsBracketQuotedString()}"));
            string leasesTableName = string.Format(CultureInfo.InvariantCulture, LeasesTableNameFormat, $"{this._userFunctionId}_{userTableId}");
            string getUnprocessedChangesQuery = $@"
                {AppLockStatements}

                DECLARE @last_sync_version bigint;
                SELECT @last_sync_version = LastSyncVersion
                FROM {GlobalStateTableName}
                WHERE UserFunctionID = '{this._userFunctionId}' AND UserTableID = {userTableId};

                SELECT COUNT_BIG(*)
                FROM CHANGETABLE(CHANGES {this._userTable.BracketQuotedFullName}, @last_sync_version) AS c
                LEFT OUTER JOIN {leasesTableName} AS l ON {leasesTableJoinCondition}
                WHERE
                    (l.{LeasesTableLeaseExpirationTimeColumnName} IS NULL AND
                       (l.{LeasesTableChangeVersionColumnName} IS NULL OR l.{LeasesTableChangeVersionColumnName} < c.{SysChangeVersionColumnName}) OR
                        l.{LeasesTableLeaseExpirationTimeColumnName} < SYSDATETIME()) AND
                    (l.{LeasesTableAttemptCountColumnName} IS NULL OR l.{LeasesTableAttemptCountColumnName} < {5});
            ";

            return new SqlCommand(getUnprocessedChangesQuery, connection, transaction);
        }
    }
}