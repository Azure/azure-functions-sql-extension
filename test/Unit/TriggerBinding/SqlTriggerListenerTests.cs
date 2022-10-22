// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlTriggerListenerTests
    {
        /// <summary>
        /// Verifies that the scale monitor descriptor ID is set to expected value.
        /// </summary>
        [Theory]
        [InlineData("testTableName", "testUserFunctionId", "testUserFunctionId-SqlTrigger-testTableName")]
        [InlineData("тестТаблицаИмя", "тестПользовательФункцияИд", "тестПользовательФункцияИд-SqlTrigger-тестТаблицаИмя")]
        public void ScaleMonitorDescriptor_ReturnsExpectedValue(string tableName, string userFunctionId, string expectedDescriptorId)
        {
            IScaleMonitor<SqlTriggerMetrics> monitor = GetScaleMonitor(tableName, userFunctionId);
            Assert.Equal(expectedDescriptorId, monitor.Descriptor.Id);
        }

        /// <summary>
        /// Verifies that no-scaling is requested if there are insufficient metrics available for making the scale
        /// decision.
        /// </summary>
        [Theory]
        [InlineData(null)]  // metrics == null
        [InlineData(new int[] { })]  // metrics.Length == 0
        [InlineData(new int[] { 1000, 1000, 1000, 1000 })] // metrics.Length == 4.
        public void ScaleMonitorGetScaleStatus_InsufficentMetrics_ReturnsNone(int[] unprocessedChangeCounts)
        {
            (IScaleMonitor<SqlTriggerMetrics> monitor, List<string> logMessages) = GetScaleMonitor();
            ScaleStatusContext context = GetScaleStatusContext(unprocessedChangeCounts, 0);

            ScaleStatus scaleStatus = monitor.GetScaleStatus(context);

            Assert.Equal(ScaleVote.None, scaleStatus.Vote);
            Assert.Contains("Requesting no-scaling: Insufficient metrics for making scale decision for table: 'testTableName'.", logMessages);
        }

        /// <summary>
        /// Verifies that only the most recent samples are considered for making the scale decision.
        /// </summary>
        [Theory]
        [InlineData(new int[] { 0, 0, 4, 3, 2, 0 }, 2, ScaleVote.None)]
        [InlineData(new int[] { 0, 0, 4, 3, 2, 1, 0 }, 2, ScaleVote.ScaleIn)]
        [InlineData(new int[] { 1000, 1000, 0, 1, 2, 1000 }, 1, ScaleVote.None)]
        [InlineData(new int[] { 1000, 1000, 0, 1, 2, 3, 1000 }, 1, ScaleVote.ScaleOut)]
        public void ScaleMonitorGetScaleStatus_ExcessMetrics_IgnoresExcessMetrics(int[] unprocessedChangeCounts, int workerCount, ScaleVote scaleVote)
        {
            (IScaleMonitor<SqlTriggerMetrics> monitor, _) = GetScaleMonitor();
            ScaleStatusContext context = GetScaleStatusContext(unprocessedChangeCounts, workerCount);

            ScaleStatus scaleStatus = monitor.GetScaleStatus(context);

            Assert.Equal(scaleVote, scaleStatus.Vote);
        }

        /// <summary>
        /// Verifies that scale-out is requested if the latest count of unprocessed changes is above the combined limit
        /// of all workers.
        /// </summary>
        [Theory]
        [InlineData(new int[] { 0, 0, 0, 0, 1 }, 0)]
        [InlineData(new int[] { 0, 0, 0, 0, 1001 }, 1)]
        [InlineData(new int[] { 0, 0, 0, 0, 10001 }, 10)]
        public void ScaleMonitorGetScaleStatus_LastCountAboveLimit_ReturnsScaleOut(int[] unprocessedChangeCounts, int workerCount)
        {
            (IScaleMonitor<SqlTriggerMetrics> monitor, List<string> logMessages) = GetScaleMonitor();
            ScaleStatusContext context = GetScaleStatusContext(unprocessedChangeCounts, workerCount);

            ScaleStatus scaleStatus = monitor.GetScaleStatus(context);

            Assert.Equal(ScaleVote.ScaleOut, scaleStatus.Vote);
            Assert.Contains("Requesting scale-out: Found too many unprocessed changes for table: 'testTableName' relative to the number of workers.", logMessages);
        }

        /// <summary>
        /// Verifies that no-scaling is requested if the latest count of unprocessed changes is not above the combined
        /// limit of all workers.
        /// </summary>
        [Theory]
        [InlineData(new int[] { 0, 0, 0, 0, 0 }, 0)]
        [InlineData(new int[] { 0, 0, 0, 0, 1000 }, 1)]
        [InlineData(new int[] { 0, 0, 0, 0, 10000 }, 10)]
        public void ScaleMonitorGetScaleStatus_LastCountBelowLimit_ReturnsNone(int[] unprocessedChangeCounts, int workerCount)
        {
            (IScaleMonitor<SqlTriggerMetrics> monitor, List<string> logMessages) = GetScaleMonitor();
            ScaleStatusContext context = GetScaleStatusContext(unprocessedChangeCounts, workerCount);

            ScaleStatus scaleStatus = monitor.GetScaleStatus(context);

            Assert.Equal(ScaleVote.None, scaleStatus.Vote);
            Assert.Contains("Requesting no-scaling: Found the number of unprocessed changes for table: 'testTableName' to not require scaling.", logMessages);
        }

        /// <summary>
        /// Verifies that scale-out is requested if the count of unprocessed changes is strictly increasing and may
        /// exceed the combined limit of all workers. Since the metric samples are separated by 10 seconds, the existing
        /// implementation should only consider the last three samples in its calculation.
        /// </summary>
        [Theory]
        [InlineData(new int[] { 0, 1, 500, 501, 751 }, 1)]
        [InlineData(new int[] { 0, 1, 4999, 5001, 7500 }, 10)]
        public void ScaleMonitorGetScaleStatus_CountIncreasingAboveLimit_ReturnsScaleOut(int[] unprocessedChangeCounts, int workerCount)
        {
            (IScaleMonitor<SqlTriggerMetrics> monitor, List<string> logMessages) = GetScaleMonitor();
            ScaleStatusContext context = GetScaleStatusContext(unprocessedChangeCounts, workerCount);

            ScaleStatus scaleStatus = monitor.GetScaleStatus(context);

            Assert.Equal(ScaleVote.ScaleOut, scaleStatus.Vote);
            Assert.Contains("Requesting scale-out: Found the unprocessed changes for table: 'testTableName' to be continuously increasing and may exceed the maximum limit set for the workers.", logMessages);
        }

        /// <summary>
        /// Verifies that no-scaling is requested if the count of unprocessed changes is strictly increasing but it may
        /// still stay below the combined limit of all workers. Since the metric samples are separated by 10 seconds,
        /// the existing implementation should only consider the last three samples in its calculation.
        /// </summary>
        [Theory]
        [InlineData(new int[] { 0, 1, 500, 501, 750 }, 1)]
        [InlineData(new int[] { 0, 1, 5000, 5001, 7500 }, 10)]
        public void ScaleMonitorGetScaleStatus_CountIncreasingBelowLimit_ReturnsNone(int[] unprocessedChangeCounts, int workerCount)
        {
            (IScaleMonitor<SqlTriggerMetrics> monitor, List<string> logMessages) = GetScaleMonitor();
            ScaleStatusContext context = GetScaleStatusContext(unprocessedChangeCounts, workerCount);

            ScaleStatus scaleStatus = monitor.GetScaleStatus(context);

            Assert.Equal(ScaleVote.None, scaleStatus.Vote);
            Assert.Contains("Avoiding scale-out: Found the unprocessed changes for table: 'testTableName' to be increasing but they may not exceed the maximum limit set for the workers.", logMessages);
        }

        /// <summary>
        /// Verifies that scale-in is requested if the count of unprocessed changes is strictly decreasing (or zero) and
        /// is also below the combined limit of workers after being reduced by one.
        /// </summary>
        [Theory]
        [InlineData(new int[] { 0, 0, 0, 0, 0 }, 1)]
        [InlineData(new int[] { 1, 0, 0, 0, 0 }, 1)]
        [InlineData(new int[] { 5, 4, 3, 2, 0 }, 1)]
        [InlineData(new int[] { 9005, 9004, 9003, 9002, 9000 }, 10)]
        public void ScaleMonitorGetScaleStatus_CountDecreasingBelowLimit_ReturnsScaleIn(int[] unprocessedChangeCounts, int workerCount)
        {
            (IScaleMonitor<SqlTriggerMetrics> monitor, List<string> logMessages) = GetScaleMonitor();
            ScaleStatusContext context = GetScaleStatusContext(unprocessedChangeCounts, workerCount);

            ScaleStatus scaleStatus = monitor.GetScaleStatus(context);

            Assert.Equal(ScaleVote.ScaleIn, scaleStatus.Vote);
            Assert.Contains("Requesting scale-in: Found table: 'testTableName' to be either idle or the unprocessed changes to be continuously decreasing.", logMessages);
        }

        /// <summary>
        /// Verifies that scale-in is requested if the count of unprocessed changes is strictly decreasing (or zero) but
        /// it is still above the combined limit of workers after being reduced by one.
        /// </summary>
        [Theory]
        [InlineData(new int[] { 5, 4, 3, 2, 1 }, 1)]
        [InlineData(new int[] { 9005, 9004, 9003, 9002, 9001 }, 10)]
        public void ScaleMonitorGetScaleStatus_CountDecreasingAboveLimit_ReturnsNone(int[] unprocessedChangeCounts, int workerCount)
        {
            (IScaleMonitor<SqlTriggerMetrics> monitor, List<string> logMessages) = GetScaleMonitor();
            ScaleStatusContext context = GetScaleStatusContext(unprocessedChangeCounts, workerCount);

            ScaleStatus scaleStatus = monitor.GetScaleStatus(context);

            Assert.Equal(ScaleVote.None, scaleStatus.Vote);
            Assert.Contains("Avoiding scale-in: Found the unprocessed changes for table: 'testTableName' to be decreasing but they are high enough to require all existing workers for processing.", logMessages);
        }

        /// <summary>
        /// Verifies that no-scaling is requested if the count of unprocessed changes is neither strictly increasing and
        /// nor strictly decreasing.
        /// </summary>
        [Theory]
        [InlineData(new int[] { 0, 0, 1, 2, 3 }, 1)]
        [InlineData(new int[] { 1, 1, 0, 0, 0 }, 10)]
        public void ScaleMonitorGetScaleStatus_CountNotIncreasingOrDecreasing_ReturnsNone(int[] unprocessedChangeCounts, int workerCount)
        {
            (IScaleMonitor<SqlTriggerMetrics> monitor, List<string> logMessages) = GetScaleMonitor();
            ScaleStatusContext context = GetScaleStatusContext(unprocessedChangeCounts, workerCount);

            ScaleStatus scaleStatus = monitor.GetScaleStatus(context);

            Assert.Equal(ScaleVote.None, scaleStatus.Vote);

            // Ensure that no-scaling was not requested because of other conditions.
            Assert.DoesNotContain("Avoiding scale-out: Found the unprocessed changes for table: 'testTableName' to be increasing but they may not exceed the maximum limit set for the workers.", logMessages);
            Assert.DoesNotContain("Avoiding scale-in: Found the unprocessed changes for table: 'testTableName' to be decreasing but they are high enough to require all existing workers for processing.", logMessages);
            Assert.Contains("Requesting no-scaling: Found the number of unprocessed changes for table: 'testTableName' to not require scaling.", logMessages);
        }

        private static IScaleMonitor<SqlTriggerMetrics> GetScaleMonitor(string tableName, string userFunctionId)
        {
            return new SqlTriggerListener<object>(
                "testConnectionString",
                tableName,
                userFunctionId,
                Mock.Of<ITriggeredFunctionExecutor>(),
                Mock.Of<ILogger>(),
                Mock.Of<IConfiguration>());
        }

        private static (IScaleMonitor<SqlTriggerMetrics> monitor, List<string> logMessages) GetScaleMonitor()
        {
            // Since multiple threads are not involved when computing the scale-status, it should be okay to not use
            // a thread-safe collection for storing the log messages.
            var logMessages = new List<string>();
            var mockLogger = new Mock<ILogger>();

            // Both LogInformation() and LogDebug() are extension methods. Since the extension methods are static, they
            // cannot be mocked. Hence, we need to setup callback on an inner class method that gets eventually called
            // by these methods in order to extract the log message.
            mockLogger
                .Setup(logger => logger.Log(It.IsAny<LogLevel>(), 0, It.IsAny<FormattedLogValues>(), null, It.IsAny<Func<object, Exception, string>>()))
                .Callback((LogLevel logLevel, EventId eventId, object state, Exception exception, Func<object, Exception, string> formatter) =>
                {
                    logMessages.Add(state.ToString());
                });

            IScaleMonitor<SqlTriggerMetrics> monitor = new SqlTriggerListener<object>(
                "testConnectionString",
                "testTableName",
                "testUserFunctionId",
                Mock.Of<ITriggeredFunctionExecutor>(),
                mockLogger.Object,
                Mock.Of<IConfiguration>());

            return (monitor, logMessages);
        }

        private static ScaleStatusContext GetScaleStatusContext(int[] unprocessedChangeCounts, int workerCount)
        {
            DateTime now = DateTime.UtcNow;

            // Returns metric samples separated by 10 seconds. The time-difference is essential for testing the
            // scale-out logic.
            return new ScaleStatusContext
            {
                Metrics = unprocessedChangeCounts?.Select((count, index) => new SqlTriggerMetrics
                {
                    UnprocessedChangeCount = count,
                    Timestamp = now + TimeSpan.FromSeconds(10 * index),
                }),
                WorkerCount = workerCount,
            };
        }
    }
}