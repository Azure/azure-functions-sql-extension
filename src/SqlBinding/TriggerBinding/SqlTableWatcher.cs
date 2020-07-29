using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlTableWatcher
    {
        private readonly string _table;
        private readonly string _workerTable;
        private readonly string _connectionStringSetting;
        private readonly IConfiguration _configuration;
        private Dictionary<string, string> _primaryKeys;
        // Use JSON to serialize each row into a Dictionary mapping from column name to column value
        // Then use _primaryKeys to check if a given column is a primary key by checking if it 
        // exists in the map
        private List<Dictionary<string, string>> _rows;
        public static int _batchSize = 10;
        public static int _maxDequeueCount = 5;

        public SqlTableWatcher(string table, string connectionStringSetting, IConfiguration configuration)
        {
            _table = table;
            _workerTable = "Worker_Table_" + _table;
            _connectionStringSetting = connectionStringSetting;
            _configuration = configuration;
        }

        private async Task CreateWorkerTableAsync()
        {
            var createTableCommandString = await BuildCreateTableCommandStringAsync();
            
            using (var connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
            {
                SqlCommand createTableCommand = new SqlCommand(createTableCommandString, connection);
                await connection.OpenAsync();
                await createTableCommand.ExecuteNonQueryAsync();
            }
        }

        private async Task<string> BuildCreateTableCommandStringAsync()
        {
            var primaryKeysWithTypes = string.Empty;
            var primaryKeysList = string.Empty;
            foreach (var primaryKey in _primaryKeys.Keys)
            {
                string type;
                _primaryKeys.TryGetValue(primaryKey, out type);
                primaryKeysWithTypes += primaryKey + " " + type + ",\n";
                primaryKeysList += primaryKey + ", ";
            }
            // Remove the trailing ", "
            primaryKeysList = primaryKeysList.Substring(0, primaryKeysList.Length - 2);
            var createTableString = String.Format(
                "IF OBJECT_ID(N\'dt{0}\', \'U\') IS NULL\n" +
                "BEGIN\n" +
                "CREATE TABLE {0} (\n" +
                "{1}" +
                "LeaseExpirationTime datetime2,\n" +
                "DequeueCount int,\n" +
                "VersionNumber bigint\n" +
                "PRIMARY KEY({2})\n" +
                ");\n" +
                "END",
                _workerTable, primaryKeysWithTypes, primaryKeysList);
            return createTableString;
        }

        private async Task GetPrimaryKeysAsync()
        {
            if (_primaryKeys == null)
            {
                _primaryKeys = new Dictionary<string, string>();
                var getPrimaryKeysQuery = String.Format(
                    "SELECT c.name, t.name\n" +
                    "FROM sys.indexes i\n" +
                    "INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id\n" +
                    "INNER JOIN sys.columns c ON ic.object_id = c.object_id AND c.column_id = ic.column_id\n" +
                    "INNER JOIN sys.types t ON c.user_type_id = t.user_type_id\n" +
                    "WHERE i.is_primar_key = 1 and i.object_id = OBJECT_ID(\'{0}\');",
                    _table
                    );
                SqlCommand getPrimaryKeysCommand = new SqlCommand(getPrimaryKeysQuery);
                using (var connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
                {
                    getPrimaryKeysCommand.Connection = connection;
                    using (var reader = await getPrimaryKeysCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _primaryKeys.Add(reader.GetString(0), reader.GetString(1));
                        }
                    }
                }
            }
        }

        private async Task GetPollingIntervalAsync()
        {

        }

        private async Task CheckForChangesAsync()
        {
            var primaryKeysList = string.Empty;
            var primaryKeysJoin = string.Empty;
            bool first = true;
            foreach (var key in _primaryKeys.Keys)
            {
                primaryKeysList += "c." + key + ", ";
                if (!first)
                {
                    primaryKeysJoin += " AND ";
                } 
                else
                {
                    first = false;
                }
                primaryKeysJoin += "c." + key + " = w." + key;
            }

            var getChangesQuery = String.Format(
                "DECLARE @version bigint;\n" +
                "SET @version = CHANGE_TRACKING_MIN_VALID_VERSION(OBJECT_ID(\'{0}\'));\n" +
                "(SELECT TOP {1} *\n" +
                "FROM\n" +
                "(SELECT {2}c.SYS_CHANGE_VERSION, c.SYS_CHANGE_CREATION_VERSION, c.SYS_CHANGE_OPERATION\n" +
                "c.SYS_CHANGE_COLUMNS, c.SYS_CHANGE_CONTEXT, w.LeaseExpirationTime, w.DequeueCount, w.VersionNumber\n" +
                "FROM CHANGETABLE (CHANGES {0}, @version) AS c\n" +
                "LEFT OUTER JOIN {3} AS w ON {4}) AS Changes\n" +
                "WHERE (Changes.LeaseExpirationTime IS NULL OR Changes.LeaseExpirationTime < SYSDATETIME())\n" +
                "AND (Changes.DequeueCount IS NULL OR Changes.DequeueCount < {5})\n" +
                "ORDER BY Changes.SYS_CHANGE_VERSION ASC;\n",
                _table, _batchSize, primaryKeysList, _workerTable, primaryKeysJoin, _maxDequeueCount
                );

            var acquireLeases =
                "IF NOT EXISTS (SELECT * FROM {0} WHERE {1})\n" +
                "INSERT INTO {0}\n" +
                "VALUES ({2}DATEADD({3}, {4}, SYSDATETIME()), 0, {5})\n" +
                "ELSE\n" +
                "UPDATE {0}\n" +
                "SET LeaseExpirationTime = DATEADD({3}, {4}, SYSDATETIME()), VersionNumber = {5}, DequeueCount = DequeueCount + 1\n" +
                "WHERE {1};";


            using (var connection = SqlBindingUtilities.BuildConnection(_connectionStringSetting, _configuration))
            {
                await connection.OpenAsync();
                var transaction = await connection.BeginTransactionAsync();
            }

        }

        private async Task RenewLeases()
        {

        }
    }
}
