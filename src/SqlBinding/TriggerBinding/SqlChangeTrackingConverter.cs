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
        /// <exception cref="ArgumentException">
        /// Thrown if table is null or empty
        /// </exception>
        public SqlChangeTrackingConverter(string table, string connectionStringSetting, IConfiguration configuration)
        {
            if (string.IsNullOrEmpty(table))
            {
                throw new ArgumentException("User table name cannot be null or empty");
            }
            _table = SqlBindingUtilities.ProcessTableName(table);
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
        /// <param name="whereCheck">
        /// Used to build up the query to read the associated row from the user table (<see cref="ChangeTableData.WhereCheck"/>)
        /// </param>
        /// <param name="primaryKeys">
        /// Used to determine which columns of each row in "rows" correspond to the primary keys of the user table (<see cref="ChangeTableData.PrimaryKeys"/>)
        /// </param>
        /// <returns></returns>
        public async Task<object> BuildSqlChangeTrackingEntries<T>(
            List<Dictionary<string, string>> rows,
            Dictionary<string, string> primaryKeys,
            string whereCheck)
        {
            var entries = new List<SqlChangeTrackingEntry<T>>(capacity: rows.Count);
            var cols = new List<string>();
            foreach (var row in rows)
            {
                // Not great that we're doing a SqlCommand per row, should batch this
                var entry = new SqlChangeTrackingEntry<T>(GetChangeType(row), await GetRowData<T>(row, cols, primaryKeys, whereCheck));
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
        /// SqlChangeType.Created for an insert, SqlChangeType.Changed for an update,
        /// and SqlChangeType.Deleted for a delete 
        /// </returns>
        private static SqlChangeType GetChangeType(Dictionary<string, string> row)
        {
            string changeType;
            row.TryGetValue("SYS_CHANGE_OPERATION", out changeType);
            if (changeType.Equals("I"))
            {
                return SqlChangeType.Inserted;
            }
            else if (changeType.Equals("U"))
            {
                return SqlChangeType.Updated;
            }
            else if (changeType.Equals("D"))
            {
                return SqlChangeType.Deleted;
            }
            else
            {
                throw new InvalidDataException($"Invalid change type encountered in change table row {row}");
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
        /// <param name="cols">
        /// The columns of the user table (cached to improve performance)
        /// </param>
        /// <param name="whereCheck">
        /// Used to build up the query to read the associated row from the user table (<see cref="ChangeTableData.WhereCheck"/>)
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
        private async Task<T> GetRowData<T>(
            Dictionary<string, string> row,
            List<string> cols,
            Dictionary<string, string> primaryKeys,
            string whereCheck)
        {
            // In the case that we can't read the data from the user table (for example, the change corresponds to a deleted row), 
            // we just return a POCO whose primary key fields are populated but nothing else
            Dictionary<string, string> rowDictionary = BuildDefaultDictionary(row, primaryKeys);
            using (SqlConnection connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
            {
                await connection.OpenAsync();
                using (SqlCommand getRowDataCommand = BuildAcquireRowDataCommand(row, primaryKeys, whereCheck, connection))
                {
                    using (SqlDataReader reader = await getRowDataCommand.ExecuteReaderAsync())
                    {

                        if (await reader.ReadAsync())
                        {
                            // Is there a better way to do this than first converting the dictionary to a JSON string, and
                            // then the JSON string to T?
                            rowDictionary = SqlBindingUtilities.BuildDictionaryFromSqlRow(reader, cols);
                        }
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
        /// Builds the SqlCommand for the SQL query used to read the row with the same primary key values as "row" from the user table
        /// </summary>
        /// <param name="row">
        /// The row from the change/worker tables containing the primary key values to match with row from the user table
        /// </param>
        /// <param name="primaryKeys">
        /// Used to build up the query to read the associated row from the user table (<see cref="ChangeTableData.PrimaryKeys"/>)
        /// </param>
        /// <param name="whereCheck">
        /// Used to build up the query to read the associated row from the user table (<see cref="ChangeTableData.WhereCheck"/>)
        /// </param>
        /// <param name="connection">
        /// The SqlConnection to attach to the returned command
        /// </param>
        /// <returns>The SqlCommand populated with the query and appropriate parameters</returns>
        private SqlCommand BuildAcquireRowDataCommand(
            Dictionary<string, string> row, 
            Dictionary<string, string> primaryKeys,
            string whereCheck,
            SqlConnection connection)
        {
            SqlCommand acquireDataCommand = new SqlCommand();

            SqlBindingUtilities.AddPrimaryKeyParametersToCommand(acquireDataCommand, row, primaryKeys);

            var acquireRowData =
                $"SELECT * FROM {_table}\n" +
                $"WHERE {whereCheck};";

            acquireDataCommand.CommandText = acquireRowData;
            acquireDataCommand.Connection = connection;

            return acquireDataCommand;
        }
    }
}
