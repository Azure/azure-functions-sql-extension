// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Linq;
using BenchmarkDotNet.Running;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Performance
{
    public class SqlBindingPerformance
    {
        public static void Main(string[] args)
        {
            bool runAll = args.Length == 0;

            // Start Azurite once before the tests run - there were issues
            // with it not getting cleaned up properly between test runs
            // TODO: This should be a more general thing for all tests
            var azuriteHost = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "azurite",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                }
            };
            azuriteHost.Start();

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
            }
            finally
            {
                azuriteHost.Kill(true);
                azuriteHost.Dispose();
            }

        }
    }
}