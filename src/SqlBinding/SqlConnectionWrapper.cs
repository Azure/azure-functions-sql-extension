using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;

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
