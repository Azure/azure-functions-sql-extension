// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Data.SqlClient;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    public class SqlConnectionWrapper
    {
        private readonly SqlConnection _connection;

        public SqlConnectionWrapper()
        {
            _connection = null;
        }
        
        public SqlConnectionWrapper(string connectionString)
        {
            _connection = new SqlConnection(connectionString);
        }

        public void SetCredential(SqlCredential credential)
        {
            if (_connection != null)
            {
                _connection.Credential = credential;
            }
        }

        public SqlConnection GetConnection()
        {
            return _connection;
        }
    }
}
