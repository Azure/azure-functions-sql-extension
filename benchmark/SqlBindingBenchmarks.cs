// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Benchmark
{
    public class SqlBindingBenchmarks
    {
        public static void Main()
        {
            BenchmarkRunner.Run<SqlInputBindingBenchmarks>(
                ManualConfig
                    .Create(DefaultConfig.Instance)
                    .WithOptions(ConfigOptions.DisableOptimizationsValidator));

            BenchmarkRunner.Run<SqlOutputBindingBenchmarks>(
                ManualConfig
                    .Create(DefaultConfig.Instance)
                    .WithOptions(ConfigOptions.DisableOptimizationsValidator));
        }
    }
}