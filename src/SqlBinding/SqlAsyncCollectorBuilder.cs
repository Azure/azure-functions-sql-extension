// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlAsyncCollectorBuilder<T> : IConverter<SqlAttribute, IAsyncCollector<T>>
    {
        private readonly IConfiguration _configuration;
        public SqlAsyncCollectorBuilder(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        IAsyncCollector<T> IConverter<SqlAttribute, IAsyncCollector<T>>.Convert(SqlAttribute attribute)
        {
            return new SqlAsyncCollector<T>(_configuration, attribute);
        }
    }
}
