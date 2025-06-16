// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlTriggerListenerTests
    {
        [Fact]
        public async Task StartAsync_ThrowsIfAlreadyStarted()
        {
            SqlTriggerListener<object> listener = CreateListener();
            // Simulate already started
            typeof(SqlTriggerListener<object>)
                .GetField("_listenerState", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(listener, 2);

            await Assert.ThrowsAsync<InvalidOperationException>(() => listener.StartAsync(CancellationToken.None));
        }

        [Fact]
        public void GetUserTableColumns_ThrowsOnUserDefinedType()
        {
            var mockLogger = new Mock<ILogger>();
            SqlTriggerListener<object> listener = CreateListener(logger: mockLogger.Object);

            var sqlConnection = new SqlConnection();
            var mockReader = new Mock<IDataReader>();
            mockReader.SetupSequence(r => r.Read())
                .Returns(true)
                .Returns(false);
            mockReader.Setup(r => r.GetString(0)).Returns("MyColumn");
            mockReader.Setup(r => r.GetString(1)).Returns("MyType");
            mockReader.Setup(r => r.GetBoolean(2)).Returns(true);

            var mockCommand = new Mock<IDbCommand>();
            mockCommand.Setup(c => c.ExecuteReader()).Returns(mockReader.Object);

            // Use reflection to call the private method
            MethodInfo method = typeof(SqlTriggerListener<object>).GetMethod("GetUserTableColumns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(listener, new object[] { sqlConnection, 1, CancellationToken.None })
            );
        }

        [Fact]
        public void GetUserTableColumns_ThrowsOnReservedColumnName()
        {
            var mockLogger = new Mock<ILogger>();
            SqlTriggerListener<object> listener = CreateListener(logger: mockLogger.Object);

            var sqlConnection = new SqlConnection();
            var mockReader = new Mock<IDataReader>();
            mockReader.SetupSequence(r => r.Read())
                .Returns(true)
                .Returns(false);
            mockReader.Setup(r => r.GetString(0)).Returns("SYS_CHANGE_VERSION"); // Reserved name
            mockReader.Setup(r => r.GetString(1)).Returns("int");
            mockReader.Setup(r => r.GetBoolean(2)).Returns(false);

            var mockCommand = new Mock<IDbCommand>();
            mockCommand.Setup(c => c.ExecuteReader()).Returns(mockReader.Object);

            // Use reflection to call the private method
            MethodInfo method = typeof(SqlTriggerListener<object>).GetMethod("GetUserTableColumns", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(listener, new object[] { sqlConnection, 1, CancellationToken.None })
            );
        }

        private static SqlTriggerListener<object> CreateListener(ILogger logger = null)
        {
            return new SqlTriggerListener<object>(
                "Server=.;Database=Test;Trusted_Connection=True;",
                "TestTable",
                null,
                "funcId",
                "oldFuncId",
                Mock.Of<ITriggeredFunctionExecutor>(),
                new SqlOptions(),
                logger ?? Mock.Of<ILogger>(),
                new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).Build()
            );
        }
    }
}