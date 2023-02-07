// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public static class ProductsTriggerWithValidation
    {
        /// <summary>
        /// Simple trigger function with additional logic to allow for verifying that the expected number
        /// of changes was recieved in each batch.
        /// </summary>
        [FunctionName(nameof(ProductsTriggerWithValidation))]
        public static void Run(
            [SqlTrigger("[dbo].[Products]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> changes,
            ILogger logger)
        {
            string expectedBatchSize = Environment.GetEnvironmentVariable("TEST_EXPECTED_BATCH_SIZE");
            if (!string.IsNullOrEmpty(expectedBatchSize) && int.Parse(expectedBatchSize) != changes.Count)
            {
                throw new Exception($"Invalid batch size, got {changes.Count} changes but expected {expectedBatchSize}");
            }
            // The output is used to inspect the trigger binding parameter in test methods.
            logger.LogInformation("SQL Changes: " + JsonConvert.SerializeObject(changes));
        }
    }
}
