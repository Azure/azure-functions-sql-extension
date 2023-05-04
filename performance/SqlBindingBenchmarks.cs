// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Linq;
using BenchmarkDotNet.Running;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Performance
{
    public class SqlBindingPerformance
    {
        public static void Main(string[] args)
        {
            bool runAll = args.Length == 0;

            Process azuriteHost = TestUtils.StartAzurite();
            TestUtils.SetupDatabase(out string masterConnectionString, out string connectionString);
            try
            {
                // **IMPORTANT** If changing these make sure to update template-steps-performance.yml as well
                if (runAll || args.Contains("input"))
                {
                    BenchmarkRunner.Run<SqlInputBindingPerformance>();
                }
                if (runAll || args.Contains("output"))
                {
                    BenchmarkRunner.Run<SqlOutputBindingPerformance>();
                }
                if (runAll || args.Contains("trigger"))
                {
                    BenchmarkRunner.Run<SqlTriggerBindingPerformance>();
                }
                if (runAll || args.Contains("trigger_batch"))
                {
                    BenchmarkRunner.Run<SqlTriggerBindingPerformance_BatchOverride>();
                }
                if (runAll || args.Contains("trigger_poll"))
                {
                    BenchmarkRunner.Run<SqlTriggerBindingPerformance_PollingIntervalOverride>();
                }
                if (runAll || args.Contains("trigger_overrides"))
                {
                    BenchmarkRunner.Run<SqlTriggerPerformance_Overrides>();
                }
                if (runAll || args.Contains("trigger_parallel"))
                {
                    BenchmarkRunner.Run<SqlTriggerBindingPerformance_Parallelization>();
                }
                if (runAll || args.Contains("trigger_changerate"))
                {
                    BenchmarkRunner.Run<SqlTriggerBindingPerformance_ChangeRate>();
                }
            }
            finally
            {
                TestUtils.StopAzurite(azuriteHost);
                TestUtils.DropDatabase(masterConnectionString, connectionString);

            }
        }
    }
}