// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.TriggerBindingSamples
{
    public static class ProductsTrigger
    {
        [FunctionName("ProductsTrigger")]
        public static void Run(
            [SqlTrigger("[dbo].[Products]", ConnectionStringSetting = "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> changes,
            ILogger logger)
        {
            foreach (SqlChange<Product> change in changes)
            {
                Product product = change.Item;
                logger.LogInformation($"Change occurred to Products table row: {change.Operation}");
                logger.LogInformation($"ProductID: {product.ProductID}, Name: {product.Name}, Cost: {product.Cost}");
            }
        }
    }
}
