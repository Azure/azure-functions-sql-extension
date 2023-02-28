// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    /// <summary>
    /// Used to ensure correct functionality with multiple user functions tracking the same table.
    /// </summary>
    public static class MultiFunctionTrigger
    {
        [FunctionName(nameof(MultiFunctionTrigger1))]
        public static void MultiFunctionTrigger1(
            [SqlTrigger("[dbo].[Products]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> products,
            ILogger logger)
        {
            logger.LogInformation("Trigger1 Changes: " + JsonConvert.SerializeObject(products));
        }

        [FunctionName(nameof(MultiFunctionTrigger2))]
        public static void MultiFunctionTrigger2(
            [SqlTrigger("[dbo].[Products]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> products,
            ILogger logger)
        {
            logger.LogInformation("Trigger2 Changes: " + JsonConvert.SerializeObject(products));
        }
    }
}
