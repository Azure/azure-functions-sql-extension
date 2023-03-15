// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.SamplesOutOfProc.TriggerBindingSamples
{
    public class ProductsTrigger
    {
        /* [Function(nameof(ProductsTrigger))]
        public static void Run(
            [SqlTrigger("[dbo].[Products]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> changes)
        {
            // The output is used to inspect the trigger binding parameter in test methods.
            if (changes.Count > 0)
            {
                Console.WriteLine(JsonConvert.SerializeObject(changes));
            }
        } */
        private static Action<ILogger, string, Exception> _loggerMessage;

        public ProductsTrigger()
        {
            _loggerMessage = LoggerMessage.Define<string>(LogLevel.Information, eventId: new EventId(0, "INFO"), formatString: "{Message}");
        }

        [Function("ProductsTrigger")]
        public static void Run(
            [SqlTrigger("[dbo].[Products]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> changes, ILogger logger)
        {
            // The output is used to inspect the trigger binding parameter in test methods.
            _loggerMessage(logger, "SQL Changes: " + JsonConvert.SerializeObject(changes), null);
        }
    }
}
