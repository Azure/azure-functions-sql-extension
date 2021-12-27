// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.OutputBindingSamples;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Performance
{
    [Collection("PerformanceTests")]
    public class SqlOutputBindingPerformanceTests : IntegrationTestBase
    {
        public SqlOutputBindingPerformanceTests(ITestOutputHelper output) : base(output)
        {
        }

        private async Task<HttpResponseMessage> SendOutputRequest(string functionName, IDictionary<string, string> query = null)
        {
            string requestUri = $"http://localhost:{this.Port}/api/{functionName}";

            if (query != null)
            {
                requestUri = QueryHelpers.AddQueryString(requestUri, query);
            }

            return await this.SendGetRequest(requestUri);
        }

        [Fact]
        public void AddProductPerformanceTest()
        {
            this.StartFunctionHost(nameof(AddProduct));

            int id = 0;
            string name = "test";
            int cost = 100;

            int numOperations = 100;
            long totalTime = 0;
            long maxTime = 0;
            long minTime = int.MaxValue;
            int indexFastest = -1;
            int indexSlowest = -1;

            var time100Operations = Stopwatch.StartNew();

            for (int i = 0; i <= numOperations; i++)
            {
                long time = 0;
                var timePerOperation = Stopwatch.StartNew();

                var query = new Dictionary<string, string>()
                {
                    { "id", id.ToString() },
                    { "name", name },
                    { "cost", cost.ToString() }
                };

                this.SendOutputRequest(nameof(AddProduct), query).Wait();

                timePerOperation.Stop();
                time = timePerOperation.ElapsedMilliseconds;

                // Update operation statistics
                if (maxTime < time)
                {
                    indexSlowest = i;
                    maxTime = time;
                }
                if (minTime > time)
                {
                    indexFastest = i;
                    minTime = time;
                }
            }

            time100Operations.Stop();
            totalTime = time100Operations.ElapsedMilliseconds;

            this.TestOutput.WriteLine("  Slowest time:  #{0}/{1} = {2} ms",
                indexSlowest, numOperations, maxTime);
            this.TestOutput.WriteLine("  Fastest time:  #{0}/{1} = {2} ms",
                indexFastest, numOperations, minTime);
            this.TestOutput.WriteLine("  Average time:  {0} ms", totalTime / numOperations);
            this.TestOutput.WriteLine("  Total time looping through {0} operations: {1} ms", numOperations, totalTime);
        }
    }
}
