// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlTriggerBindingTests
    {
        [Fact]
        public async Task SqlTriggerBindingProvider_ReturnsNullBindingForParameterWithoutAttribute()
        {
            Type parameterType = typeof(IReadOnlyList<SqlChange<object>>);
            ITriggerBinding binding = await CreateTriggerBindingAsync(parameterType, nameof(UserFunctionWithoutAttribute));
            Assert.Null(binding);
        }

        [Fact]
        public async Task SqlTriggerBindingProvider_ThrowsForMissingConnectionString()
        {
            Type parameterType = typeof(IReadOnlyList<SqlChange<object>>);
            Task testCode() { return CreateTriggerBindingAsync(parameterType, nameof(UserFunctionWithoutConnectionString)); }
            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(testCode);

            Assert.Equal(
                "Must specify ConnectionStringSetting, which should refer to the name of an app setting that contains a SQL connection string",
                exception.Message);
        }

        [Theory]
        [InlineData(typeof(object))]
        [InlineData(typeof(SqlChange<object>))]
        [InlineData(typeof(IEnumerable<SqlChange<object>>))]
        [InlineData(typeof(IReadOnlyList<object>))]
        [InlineData(typeof(IReadOnlyList<IReadOnlyList<object>>))]
        public async Task SqlTriggerBindingProvider_ThrowsForInvalidTriggerParameterType(Type parameterType)
        {
            Task testCode() { return CreateTriggerBindingAsync(parameterType, nameof(UserFunctionWithoutConnectionString)); }
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(testCode);

            Assert.Equal(
                $"Can't bind SqlTriggerAttribute to type {parameterType}. Only IReadOnlyList<SqlChange<T>> is supported, where T is the type of user-defined POCO that matches the schema of the user table",
                exception.Message);
        }

        [Fact]
        public async Task SqlTriggerBindingProvider_ReturnsBindingForValidTriggerParameterType()
        {
            Type parameterType = typeof(IReadOnlyList<SqlChange<object>>);
            ITriggerBinding binding = await CreateTriggerBindingAsync(parameterType, nameof(UserFunctionWithAttribute));
            Assert.NotNull(binding);
        }

        private static async Task<ITriggerBinding> CreateTriggerBindingAsync(Type parameterType, string methodName)
        {
            var provider = new SqlTriggerBindingProvider(
                Mock.Of<IConfiguration>(c => c["dummyConnectionStringSetting"] == "dummyConnectionString"),
                Mock.Of<IHostIdProvider>(),
                Mock.Of<ILoggerFactory>(f => f.CreateLogger(It.IsAny<string>()) == Mock.Of<ILogger>()));

            // Possibly the simplest way to construct a ParameterInfo object.
            ParameterInfo parameter = typeof(SqlTriggerBindingTests)
                .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(parameterType)
                .GetParameters()[0];

            return await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));
        }

        private static void UserFunctionWithoutAttribute<T>(T _) { }

        private static void UserFunctionWithoutConnectionString<T>([SqlTrigger("dummyTableName")] T _) { }

        private static void UserFunctionWithAttribute<T>([SqlTrigger("dummyTableName", ConnectionStringSetting = "dummyConnectionStringSetting")] T _) { }
    }
}