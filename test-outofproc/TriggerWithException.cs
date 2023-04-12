// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using DotnetIsolatedTests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;

namespace DotnetIsolatedTests
{
    public static class TriggerWithException
    {
        public const string ExceptionMessage = "TriggerWithException test exception";
        private static bool threwException = false;
        private static readonly Action<ILogger, string, Exception> _loggerMessage = LoggerMessage.Define<string>(LogLevel.Information, eventId: new EventId(0, "INFO"), formatString: "{Message}");

        /// <summary>
        /// Used in verification that exceptions thrown by functions cause the trigger to retry calling the function
        /// once the lease timeout has expired
        /// </summary>
        [Function(nameof(TriggerWithException))]
        public static void Run(
            [SqlTrigger("[dbo].[Products]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> changes,
            FunctionContext context)
        {
            if (!threwException)
            {
                threwException = true;
                throw new InvalidOperationException(ExceptionMessage);
            }
            _loggerMessage(context.GetLogger("TriggerWithException"), "SQL Changes: " + Utils.JsonSerializeObject(changes), null);

        }
    }
}
