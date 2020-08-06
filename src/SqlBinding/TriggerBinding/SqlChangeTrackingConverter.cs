// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlChangeTrackingConverter
    {
        private readonly string _table;
        private readonly string _connectionStringSetting;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlChangeTrackingConverter"/> class.
        /// </summary>
        /// <param name="connectionStringSetting"> 
        /// The name of the app setting that stores the SQL connection string
        /// </param>
        /// <param name="table"> 
        /// The name of the user table that changes are being tracked on
        /// </param>
        /// <param name="configuration">
        /// Used to extract the connection string from connectionStringSetting
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of the parameters are null
        /// </exception>
        public SqlChangeTrackingConverter(string table, string connectionStringSetting, IConfiguration configuration)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _connectionStringSetting = connectionStringSetting ?? throw new ArgumentNullException(nameof(connectionStringSetting));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Returns a list of SqlChangeTrackingEntry. Each entry is populated with the type of the change
        /// and the associated data from the user table for each row in rows
        /// </summary>
        /// <typeparam name="T">
        /// The POCO representing a row of the user's table
        /// </typeparam>
        /// <param name="rows">
        /// The list of rows that were changed. Each row is populated by its primary key values,
        /// as well as all columns from both the worker table and change table (<see cref="ChangeTableData.WorkerTableRows"/>)
        /// </param>
        /// <param name="whereChecksOfRows">
        /// Used to build up the query to read the associated row from the user table (<see cref="ChangeTableData.WhereChecksOfRows"/>)
        /// </param>
        /// <param name="primaryKeys">
        /// Used to determine which columns of each row in "rows" correspond to the primary keys of the user table (<see cref="ChangeTableData.PrimaryKeys"/>)
        /// </param>
        /// <returns></returns>
        public async Task<object> BuildSqlChangeTrackingEntries<T>(List<Dictionary<String, String>> rows, 
            Dictionary<Dictionary<string, string>, string> whereChecksOfRows, Dictionary<string, string> primaryKeys)
        {
            var entries = new List<SqlChangeTrackingEntry<T>>();
            foreach (var row in rows)
            {
                var changeType = GetChangeType(row);
                var entry = new SqlChangeTrackingEntry<T>
                {
                    ChangeType = changeType,
                    // Not great that we're doing a SqlCommand per row, should batch this
                    Data = await GetRowData<T>(row, changeType, new List<string>(), whereChecksOfRows, primaryKeys)
                };
                entries.Add(entry);
            }
            return entries;
        }

        /// <summary>
        /// Gets the change associated with this row (either an insert, update or delete)
        /// </summary>
        /// <param name="row">
        /// The (combined) row from the change table and worker table
        /// </param>
        /// <returns>
        /// WatcherChangeTypes.Created for an insert, WatcherChangeTypes.Changed for an update,
        /// and WatcherChangeTypes.Deleted for a delete 
        /// </returns>
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

        /// <summary>
        /// Gets the current data from the user table associated with the primary key values of "row". Only
        /// gets the data if the row was inserted or updated, since a deleted row no longer exists in the user table
        /// </summary>
        /// <typeparam name="T">
        /// The POCO representing a row of the user's table
        /// </typeparam>
        /// <param name="row">
        /// The row containing the primary key value used to retrieve the associated row from the user table
        /// </param>
        /// <param name="changeType">
        /// The type of change, used to determine if the associated row still exists in the user table
        /// </param>
        /// <param name="cols">
        /// The columns of the user table (cached to improve performance)
        /// </param>
        /// <param name="whereChecksOfRows">
        /// Used to build up the query to read the associated row from the user table (<see cref="ChangeTableData.whereChecksOfRows"/>)
        /// </param>
        /// <param name="primaryKeys">
        /// Used to determine which columns of each row in "rows" correspond to the primary keys of the user table (<see cref="ChangeTableData.PrimaryKeys"/>)
        /// </param>
        /// <returns>
        /// The row from the user table for an insert or update, or the default value of T for a delete
        /// The result could also be empty in the case that we are processing a change that does not reflect the current state of the user
        /// table. For example, the most recent change to the row could be that it was deleted, but the method is processing an older change
        /// in which it was updated. In that case, attempting to get the data from the user table will also fail
        /// </returns>
        private async Task<T> GetRowData<T>(Dictionary<string, string> row, WatcherChangeTypes changeType, List<string> cols,
            Dictionary<Dictionary<string, string>, string> whereChecksOfRows, Dictionary<string, string> primaryKeys)
        {
            // In the case that we can't read the data from the user table (for example, the change corresponds to a deleted row), 
            // we just return a POCO whose primary key fields are populated but nothing else
            var rowDictionary = BuildDefaultDictionary(row, primaryKeys);
            using (var connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
            {
                await connection.OpenAsync();
                var getRowDataCommand = new SqlCommand(BuildAcquireRowDataString(row, whereChecksOfRows), connection);
                using (var reader = await getRowDataCommand.ExecuteReaderAsync())
                {
                    
                    while (await reader.ReadAsync())
                    {
                        // Is there a better way to do this than first converting the dictionary to a JSON string, and
                        // then the JSON string to T?
                        rowDictionary = SqlBindingUtilities.BuildDictionaryFromSqlRow(reader, cols);
                    }
                }
            }
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(rowDictionary));
        }

        /// <summary>
        /// Builds up a default POCO in which only the fields corresponding to the primary keys are populated
        /// </summary>
        /// <param name="row">
        /// Contains the values of the primary keys that the POCO is populated with
        /// </param>
        /// <param name="primaryKeys">
        /// Used to determine which columns in "row" correspond to the primary keys of the user's table (and thus of the POCO)
        /// </param>
        /// <returns>The default POCO</returns>
        private static Dictionary<string, string> BuildDefaultDictionary(Dictionary<string, string> row, Dictionary<string, string> primaryKeys)
        {
            var defaultDictionary = new Dictionary<string, string>();
            foreach (var primaryKey in primaryKeys.Keys)
            {
                string primaryKeyValue;
                row.TryGetValue(primaryKey, out primaryKeyValue);
                defaultDictionary.Add(primaryKey, primaryKeyValue);
            }
            return defaultDictionary;
        }

        /// <summary>
        /// Builds the string for the SQL query used to read the row with the same primary key values as "row" from the user table
        /// </summary>
        /// <param name="row">
        /// The row from the change/worker tables containing the primary key values to match with row from the user table
        /// </param>
        /// <param name="whereChecksOfRows">
        /// Used to build up the query to read the associated row from the user table (<see cref="ChangeTableData.whereChecksOfRows"/>)
        /// </param>
        /// <returns>
        /// The SQL query
        /// </returns>
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
