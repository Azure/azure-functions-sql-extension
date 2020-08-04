using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    // Will the BindingConfigProvider instantiate one of these per function? In that case it should be safe to save the table name, right?
    // And the connection and stuff? Should also check one of each of the other classes is created per function so that saving connection 
    // information is fine. That seems to be the case given that that's what the CosmosDB binding does, and the fact that you get the 
    // attribute and stuff that triggered the call. But should check.
    internal class SqlChangeTrackingConverter
    {
        private readonly string _table;
        private readonly string _connectionStringSetting;
        private readonly IConfiguration _configuration;

        public SqlChangeTrackingConverter(string table, string connectionStringSetting, IConfiguration configuration)
        {
            _table = table;
            _connectionStringSetting = connectionStringSetting;
            _configuration = configuration;
        }

        // Should make this use the WorkerTableRow type eventually, but for now keep as dictionary
        public async Task<object> BuildSqlChangeTrackingEntries<T>(List<Dictionary<String, String>> rows, 
            Dictionary<Dictionary<string, string>, string> whereChecksOfRows)
        {
            var entries = new List<SqlChangeTrackingEntry<T>>();
            foreach (var row in rows)
            {
                var changeType = GetChangeType(row);
                var entry = new SqlChangeTrackingEntry<T>
                {
                    ChangeType = changeType,
                    Data = await GetRowData<T>(row, changeType, new List<string>(), whereChecksOfRows)
                };
                entries.Add(entry);
            }
            return entries;
        }

        private WatcherChangeTypes GetChangeType(Dictionary<string, string> row)
        {
            string changeType;
            row.TryGetValue("SYS_CHANGE_OPERATION", out changeType);
            if (changeType.Equals("I"))
            {
                return WatcherChangeTypes.Created;
            }
            else if (changeType.Equals("U"))
            {
                return WatcherChangeTypes.Changed;
            }
            else if (changeType.Equals("D"))
            {
                return WatcherChangeTypes.Deleted;
            }
            else
            {
                throw new InvalidDataException(String.Format("Invalid change type encountered in change table row {0}", row));
            }
        }

        // Not great that we're doing a SqlCommand per row, should batch this
        private async Task<T> GetRowData<T>(Dictionary<string, string> row, WatcherChangeTypes changeType, List<string> cols,
            Dictionary<Dictionary<string, string>, string> whereChecksOfRows)
        {
            // Can't retrieve the data of a deleted row. Though we could still fail to get the row data if, for example,
            // the row was deleted in a later change than we are currently processing. So should probably have a case for this
            if (changeType != WatcherChangeTypes.Deleted)
            {
                using (var connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
                {
                    await connection.OpenAsync();
                    var getRowDataCommand = new SqlCommand(BuildAcquireRowDataString(row, whereChecksOfRows), connection);
                    using (var reader = await getRowDataCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var rowDictionary = SqlBindingUtilities.BuildDictionaryFromSqlRow(reader, cols);
                            // Is there a better way to do this than first converting the dictionary to a JSON string, and
                            // then the JSON string to T?
                            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(rowDictionary));
                        }
                    }
                }
            }
            return default(T);
        }

        private string BuildAcquireRowDataString(Dictionary<string, string> row, Dictionary<Dictionary<string, string>, string> whereChecksOfRows)
        {
            var acquireRowData =
                "SELECT * FROM {0}\n" +
                "WHERE {1};";
            string whereCheck;
            // Should maybe be throwing exceptions if these ever fail
            whereChecksOfRows.TryGetValue(row, out whereCheck);

            return String.Format(acquireRowData, _table, whereCheck);
        }
    }
}
