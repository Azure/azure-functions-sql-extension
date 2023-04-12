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
    /// <summary>
    /// Used to ensure correct functionality with multiple user functions tracking the same table.
    /// </summary>
    public static class MultiFunctionTrigger
    {
        private static readonly Action<ILogger, string, Exception> _loggerMessage = LoggerMessage.Define<string>(LogLevel.Information, eventId: new EventId(0, "INFO"), formatString: "{Message}");

        [Function(nameof(MultiFunctionTrigger1))]
        public static void MultiFunctionTrigger1(
            [SqlTrigger("[dbo].[Products]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> products,
            FunctionContext context)
        {
            _loggerMessage(context.GetLogger("ProductsTriggerWithValidation"), "Trigger1 Changes: " + Utils.JsonSerializeObject(products), null);
        }

        [Function(nameof(MultiFunctionTrigger2))]
        public static void MultiFunctionTrigger2(
            [SqlTrigger("[dbo].[Products]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> products,
            FunctionContext context)
        {
            _loggerMessage(context.GetLogger("ProductsTriggerWithValidation"), "Trigger2 Changes: " + Utils.JsonSerializeObject(products), null);
        }
    }
}
