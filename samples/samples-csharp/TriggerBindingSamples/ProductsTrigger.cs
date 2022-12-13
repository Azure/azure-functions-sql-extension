// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.TriggerBindingSamples
{
    public static class ProductsTrigger
    {
        [FunctionName(nameof(ProductsTrigger))]
        public static void Run(
            [SqlTrigger("[dbo].[Products]", ConnectionStringSetting = "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> changes,
            ILogger logger)
        {
            // The output is used to inspect the trigger binding parameter in test methods.
            logger.LogInformation("SQL Changes: " + Utils.SerializeObject(changes));
        }
    }
}
