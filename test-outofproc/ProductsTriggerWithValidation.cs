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
    public static class ProductsTriggerWithValidation
    {
        /// <summary>
        /// Simple trigger function with additional logic to allow for verifying that the expected number
        /// of changes was received in each batch.
        /// </summary>
        [Function(nameof(ProductsTriggerWithValidation))]
        public static void Run(
            [SqlTrigger("[dbo].[Products]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> changes,
            FunctionContext context)
        {
            ILogger logger = context.GetLogger("ProductsTriggerWithValidation");
            string expectedMaxBatchSize = Environment.GetEnvironmentVariable("TEST_EXPECTED_MAX_BATCH_SIZE");
            if (!string.IsNullOrEmpty(expectedMaxBatchSize) && int.Parse(expectedMaxBatchSize, null) != changes.Count)
            {
                throw new InvalidOperationException($"Invalid max batch size, got {changes.Count} changes but expected {expectedMaxBatchSize}");
            }
            // The output is used to inspect the trigger binding parameter in test methods.
            logger.LogInformation("SQL Changes: " + Utils.JsonSerializeObject(changes), null);
        }
    }
}
