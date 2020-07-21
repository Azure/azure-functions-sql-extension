// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Sql;
using Microsoft.Azure.WebJobs.Hosting;

[assembly: WebJobsStartup(typeof(SqlBindingStartup))]
namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    public class SqlBindingStartup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            builder.AddSql();
        }
    }
}