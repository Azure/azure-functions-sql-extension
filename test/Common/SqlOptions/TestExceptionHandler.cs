// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    public class TestExceptionHandlerFactory : IWebJobsExceptionHandlerFactory
    {
        private readonly TestExceptionHandler _handler = new();

        public IWebJobsExceptionHandler Create(IHost jobHost)
        {
            return this._handler;
        }
    }

    public class TestExceptionHandler : IWebJobsExceptionHandler
    {
#pragma warning disable IDE0060
        public static void Initialize(JobHost host)
        {
        }
#pragma warning restore IDE0060

        public Task OnTimeoutExceptionAsync(ExceptionDispatchInfo exceptionInfo, TimeSpan timeoutGracePeriod)
        {
            Assert.Fail($"Timeout exception in test exception handler: {exceptionInfo.SourceException}");
            return Task.CompletedTask;
        }

        public Task OnUnhandledExceptionAsync(ExceptionDispatchInfo exceptionInfo)
        {
            Assert.Fail($"Error in test exception handler: {exceptionInfo.SourceException}");
            return Task.CompletedTask;
        }
    }
}
