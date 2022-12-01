// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Performance
{
    public class SqlTriggerBindingPerformanceTestBase : SqlTriggerBindingIntegrationTestBase
    {
        [IterationCleanup]
        public void IterationCleanup()
        {
            // Delete all rows in Products table after each iteration so we start fresh each time
            this.ExecuteNonQuery("TRUNCATE TABLE Products");
            // Clear the leases table, otherwise we may end up getting blocked by leases from a previous run
            this.ExecuteNonQuery(@"DECLARE @cmd varchar(100)
            DECLARE cmds CURSOR FOR
            SELECT 'TRUNCATE TABLE az_func.' + Name + ''
            FROM sys.tables
            WHERE Name LIKE 'Leases_%'

            OPEN cmds
            WHILE 1 = 1
            BEGIN
                FETCH cmds INTO @cmd
                IF @@fetch_status != 0 BREAK
                EXEC(@cmd)
            END
            CLOSE cmds;
            DEALLOCATE cmds");
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.Dispose();
        }
    }
}