// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.TriggerBindingSamples;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using BenchmarkDotNet.Attributes;
using System.Threading;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration;
using System;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Performance
{
    [MemoryDiagnoser]
    public class SqlTriggerBindingPerformance_ChangeRate : IDisposable
    {
        private SqlTriggerBindingIntegrationTests TestObject;

        [IterationSetup]
        public void IterationSetup()
        {
            // We start a new instance each iteration because this test constantly inserts items
            // which results in more items being inserted than the test looks for (intentionally).
            // If we used the same trigger function for all runs then we run into issues with leftover
            // items from previous runs being picked up by the new function - so to get around this
            // we have to take the overhead hit of starting the function host each iteration to ensure
            // a completely clean start for each iteration.
            this.TestObject = new SqlTriggerBindingIntegrationTests();
            this.TestObject.SetChangeTrackingForTable("Products", true);
            this.TestObject.StartFunctionHost(nameof(ProductsTrigger), SupportedLanguages.CSharp);
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            this.TestObject.Dispose();
        }

        /// <summary>
        /// Runs a test with a high constant change rate. Items are inserted constantly until the test
        /// detects that the specified number of items has been processed. Note this will NOT be all
        /// of the items sent - those are being inserted at a much higher rate than the function can deal
        /// with.
        /// </summary>
        /// <param name="count">The number of items to process before ending the test</param>
        [Benchmark]
        [Arguments(1000)]
        public async Task ChangeRate(int count)
        {
            var tokenSource = new CancellationTokenSource();
            await this.TestObject.WaitForProductChanges(
                1,
                count,
                SqlChangeOperation.Insert,
                () => { this.ChangesLoop(tokenSource.Token); return Task.CompletedTask; },
                id => $"Product {id}",
                id => id * 100,
                this.TestObject.GetBatchProcessingTimeout(1, count)); // Wait for up to 30 seconds
            tokenSource.Cancel();
        }

        private void ChangesLoop(CancellationToken token)
        {
            // Start off worker to insert items but then immediately return since
            // we're only going to be watching for a subset of the inserted items
            _ = Task.Run(() =>
            {
                const int ChangesInBatch = 50;
                int startIndex = 1, endIndex = ChangesInBatch;
                while (!token.IsCancellationRequested)
                {
                    this.TestObject.InsertProducts(startIndex, endIndex);
                    startIndex += ChangesInBatch;
                    endIndex += ChangesInBatch;
                    // No specific reason for this delay except to avoid having this take
                    // up too many cycles when running the tests. 1 item/ms is fast enough
                    // for our purposes currently
                    Thread.Sleep(50);
                }
            }, token);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.TestObject.Dispose();
        }
    }
}