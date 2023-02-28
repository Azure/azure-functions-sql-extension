// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public static class TriggerWithException
    {
        public const string ExceptionMessage = "TriggerWithException test exception";
        private static bool threwException = false;

        /// <summary>
        /// Used in verification that exceptions thrown by functions cause the trigger to retry calling the function
        /// once the lease timeout has expired
        /// </summary>
        [FunctionName(nameof(TriggerWithException))]
        public static void Run(
            [SqlTrigger("[dbo].[Products]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> changes,
            ILogger logger)
        {
            if (!threwException)
            {
                threwException = true;
                throw new Exception(ExceptionMessage);
            }
            logger.LogInformation("SQL Changes: " + JsonConvert.SerializeObject(changes));
        }
    }
}
