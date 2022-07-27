// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class TriggerBindingTests
    {
        private static readonly Mock<IConfiguration> config = new Mock<IConfiguration>();
        private static readonly Mock<IHostIdProvider> hostIdProvider = new Mock<IHostIdProvider>();
        private static readonly Mock<ILoggerFactory> loggerFactory = new Mock<ILoggerFactory>();
        private static readonly Mock<ITriggeredFunctionExecutor> mockExecutor = new Mock<ITriggeredFunctionExecutor>();
        private static readonly Mock<ILogger> logger = new Mock<ILogger>();
        private static readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        [Fact]
        public void TestTriggerBindingProviderNullConfig()
        {
            Assert.Throws<ArgumentNullException>(() => new SqlTriggerAttributeBindingProvider(null, hostIdProvider.Object, loggerFactory.Object));
            Assert.Throws<ArgumentNullException>(() => new SqlTriggerAttributeBindingProvider(config.Object, null, loggerFactory.Object));
            Assert.Throws<ArgumentNullException>(() => new SqlTriggerAttributeBindingProvider(config.Object, hostIdProvider.Object, null));
        }

        [Fact]
        public async void TestTriggerAttributeBindingProviderNullContext()
        {
            var configProvider = new SqlTriggerAttributeBindingProvider(config.Object, hostIdProvider.Object, loggerFactory.Object);
            await Assert.ThrowsAsync<ArgumentNullException>(() => configProvider.TryCreateAsync(null));
        }

        [Fact]
        public void TestTriggerListenerNullConfig()
        {
            string connectionString = "testConnectionString";
            string tableName = "testTableName";
            string userFunctionId = "testUserFunctionId";

            Assert.Throws<ArgumentNullException>(() => new SqlTriggerListener<TestData>(null, tableName, userFunctionId, mockExecutor.Object, logger.Object));
            Assert.Throws<ArgumentNullException>(() => new SqlTriggerListener<TestData>(connectionString, null, userFunctionId, mockExecutor.Object, logger.Object));
            Assert.Throws<ArgumentNullException>(() => new SqlTriggerListener<TestData>(connectionString, tableName, null, mockExecutor.Object, logger.Object));
            Assert.Throws<ArgumentNullException>(() => new SqlTriggerListener<TestData>(connectionString, tableName, userFunctionId, null, logger.Object));
            Assert.Throws<ArgumentNullException>(() => new SqlTriggerListener<TestData>(connectionString, tableName, userFunctionId, mockExecutor.Object, null));
        }

        [Fact]
        public void TestTriggerBindingNullConfig()
        {
            string connectionString = "testConnectionString";
            string tableName = "testTableName";

            Assert.Throws<ArgumentNullException>(() => new SqlTriggerBinding<TestData>(null, connectionString, TriggerBindingFunctionTest.GetParamForChanges(), hostIdProvider.Object, logger.Object));
            Assert.Throws<ArgumentNullException>(() => new SqlTriggerBinding<TestData>(tableName, null, TriggerBindingFunctionTest.GetParamForChanges(), hostIdProvider.Object, logger.Object));
            Assert.Throws<ArgumentNullException>(() => new SqlTriggerBinding<TestData>(tableName, connectionString, null, hostIdProvider.Object, logger.Object));
            Assert.Throws<ArgumentNullException>(() => new SqlTriggerBinding<TestData>(tableName, connectionString, TriggerBindingFunctionTest.GetParamForChanges(), null, logger.Object));
            Assert.Throws<ArgumentNullException>(() => new SqlTriggerBinding<TestData>(tableName, connectionString, TriggerBindingFunctionTest.GetParamForChanges(), hostIdProvider.Object, null));
        }

        [Fact]
        public async void TestTriggerBindingProviderWithInvalidParameter()
        {
            var triggerBindingProviderContext = new TriggerBindingProviderContext(TriggerBindingFunctionTest.GetParamForChanges(), cancellationTokenSource.Token);
            var triggerAttributeBindingProvider = new SqlTriggerAttributeBindingProvider(config.Object, hostIdProvider.Object, loggerFactory.Object);

            //Trying to create a SqlTriggerBinding with IEnumerable<SqlChange<T>> type for the changes
            //This is expected to throw an exception as the type expected for receiving the changes is IReadOnlyList<SqlChange<T>>
            await Assert.ThrowsAsync<InvalidOperationException>(() => triggerAttributeBindingProvider.TryCreateAsync(triggerBindingProviderContext));
        }

        ///<summary>
        /// Creating a function using trigger with wrong parameter for changes field.
        ///</summary>
        private static class TriggerBindingFunctionTest
        {
            ///<summary>
            ///Example function created with wrong parameter
            ///</summary>
            public static void InvalidParameterType(
            [SqlTrigger("[dbo].[Employees]", ConnectionStringSetting = "SqlConnectionString")]
            IEnumerable<SqlChange<TestData>> changes,
            ILogger logger)
            {
                logger.LogInformation(changes.ToString());
            }
            ///<summary>
            ///Gets the parameter info for changes in the function
            ///</summary>
            public static ParameterInfo GetParamForChanges()
            {
                MethodInfo methodInfo = typeof(TriggerBindingFunctionTest).GetMethod("InvalidParameterType", BindingFlags.Public | BindingFlags.Static);
                ParameterInfo[] parameters = methodInfo.GetParameters();
                return parameters[^2];
            }
        }
    }
}