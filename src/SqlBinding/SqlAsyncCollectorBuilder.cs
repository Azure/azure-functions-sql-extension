// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlAsyncCollectorBuilder<T> : IConverter<SqlAttribute, IAsyncCollector<T>>
    {
        private readonly IConfiguration _configuration;
        private SqlAsyncCollector<T> _collector;

        public SqlAsyncCollectorBuilder(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        IAsyncCollector<T> IConverter<SqlAttribute, IAsyncCollector<T>>.Convert(SqlAttribute attribute)
        {
            if (_collector == null)
            {
                _collector = new SqlAsyncCollector<T>(_configuration, attribute);
            }
            return _collector;
        }
    }
}
