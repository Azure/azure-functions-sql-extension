// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Benchmark
{
    public class Program
    {
        public static void Main()
        {
            BenchmarkRunner.Run<InputBindingBenchmarks>(
                ManualConfig
                    .Create(DefaultConfig.Instance)
                    .WithOptions(ConfigOptions.DisableOptimizationsValidator));
        }
    }
}