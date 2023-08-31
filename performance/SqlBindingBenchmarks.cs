// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;
using BenchmarkDotNet.Running;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Performance
{
    public class SqlBindingPerformance
    {
        public static void Main(string[] args)
        {
            bool runAll = args.Length == 0;

            using var testFixture = new IntegrationTestFixture();

            // **IMPORTANT** If changing these make sure to update template-steps-performance.yml as well
            if (runAll || args.Contains("input"))
            {
                BenchmarkRunner.Run<SqlInputBindingPerformance>();
            }
            if (runAll || args.Contains("output"))
            {
                BenchmarkRunner.Run<SqlOutputBindingPerformance>();
            }
        }
    }
}