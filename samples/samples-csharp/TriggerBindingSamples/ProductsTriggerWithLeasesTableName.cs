// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.TriggerBindingSamples
{
    public static class ProductsTriggerWithLeasesTableName
    {
        [FunctionName(nameof(ProductsTriggerWithLeasesTableName))]
        public static void Run(
            [SqlTrigger("[dbo].[Products]", "SqlConnectionString", "Leases")]
            IReadOnlyList<SqlChange<Product>> changes,
            ILogger logger)
        {
            logger.LogInformation("SQL Changes: " + JsonConvert.SerializeObject(changes));
        }
    }
}
