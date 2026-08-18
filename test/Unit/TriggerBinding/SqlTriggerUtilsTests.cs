// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlTriggerUtilsTests
    {
        [Fact]
        public async Task RunStartupPhaseAsync_RunsActionAndLogsPhase()
        {
            (Mock<ILogger> mockLogger, List<string> logMessages) = CreateMockLogger();
            bool actionRun = false;

            await SqlTriggerUtils.RunStartupPhaseAsync("TestPhase", "dbo.Products", "testFunctionId", mockLogger.Object, () =>
            {
                actionRun = true;
                return Task.CompletedTask;
            });

            Assert.True(actionRun);
            Assert.Equal(2, logMessages.Count);
            Assert.Equal("SQL trigger startup phase 'TestPhase' started for table: 'dbo.Products', function ID: 'testFunctionId'.", logMessages[0]);
            Assert.StartsWith("SQL trigger startup phase 'TestPhase' completed for table: 'dbo.Products', function ID: 'testFunctionId' in ", logMessages[1]);
            Assert.EndsWith("ms.", logMessages[1]);
        }

        [Fact]
        public async Task RunStartupPhaseAsync_LogsPhaseFailureAndRethrows()
        {
            (Mock<ILogger> mockLogger, List<string> logMessages) = CreateMockLogger();
            var expectedException = new InvalidOperationException("Test failure");

            InvalidOperationException actualException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                SqlTriggerUtils.RunStartupPhaseAsync("FailingPhase", "dbo.Products", "testFunctionId", mockLogger.Object, () => throw expectedException));

            Assert.Same(expectedException, actualException);
            Assert.Equal(2, logMessages.Count);
            Assert.Equal("SQL trigger startup phase 'FailingPhase' started for table: 'dbo.Products', function ID: 'testFunctionId'.", logMessages[0]);
            Assert.StartsWith("SQL trigger startup phase 'FailingPhase' failed for table: 'dbo.Products', function ID: 'testFunctionId' after ", logMessages[1]);
            Assert.Contains("Test failure", logMessages[1]);
        }

        private static (Mock<ILogger> logger, List<string> logMessages) CreateMockLogger()
        {
            var logMessages = new List<string>();
            var mockLogger = new Mock<ILogger>();
            mockLogger
                .Setup(logger => logger.Log(It.IsAny<LogLevel>(), 0, It.IsAny<FormattedLogValues>(), null, It.IsAny<Func<object, Exception, string>>()))
                .Callback((LogLevel logLevel, EventId eventId, object state, Exception exception, Func<object, Exception, string> formatter) =>
                {
                    logMessages.Add(state.ToString());
                });

            return (mockLogger, logMessages);
        }
    }
}