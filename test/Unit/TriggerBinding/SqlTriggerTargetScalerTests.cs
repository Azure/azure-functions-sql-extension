// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlTriggerTargetScalerTests
    {
        /// <summary>
        /// Verifies that the scale result returns the expected target worker count.
        /// </summary>
        [Theory]
        [InlineData(6000, null, 6)]
        [InlineData(4500, null, 5)]
        [InlineData(1080, 100, 11)]
        [InlineData(100, null, 1)]
        public void SqlTriggerTargetScaler_Returns_Expected(int unprocessedChangeCount, int? concurrency, int expected)
        {
            var targetScaler = new SqlTriggerTargetScaler(
                "testUserFunctionId",
                "testUserTableName",
                "testConnectionString",
                SqlTriggerListener<object>.DefaultMaxChangesPerWorker,
                Mock.Of<ILogger>()
                );

            TargetScalerResult result = targetScaler.GetScaleResultInternal(concurrency ?? SqlTriggerListener<object>.DefaultMaxChangesPerWorker, unprocessedChangeCount);

            Assert.Equal(result.TargetWorkerCount, expected);
        }
    }
}