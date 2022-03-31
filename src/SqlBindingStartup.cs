// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Sql;
using Microsoft.Azure.WebJobs.Hosting;

[assembly: WebJobsStartup(typeof(SqlBindingStartup))]
namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal sealed class SqlBindingStartup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            builder.AddSql();
        }
    }
}