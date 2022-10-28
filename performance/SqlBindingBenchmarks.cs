// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using BenchmarkDotNet.Running;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Performance
{
    public class SqlBindingPerformance
    {
        public static void Main()
        {
            //BenchmarkRunner.Run<SqlInputBindingPerformance>();
            //BenchmarkRunner.Run<SqlOutputBindingPerformance>();
            BenchmarkRunner.Run<SqlTriggerBindingPerformance>();
        }
    }
}