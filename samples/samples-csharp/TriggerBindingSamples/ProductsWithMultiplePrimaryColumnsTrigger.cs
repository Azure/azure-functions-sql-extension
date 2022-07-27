// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.TriggerBindingSamples
{
    public class MultiplePrimaryKeyProduct
    {
        public int ProductID { get; set; }

        public int ExternalID { get; set; }

        public string Name { get; set; }

        public int Cost { get; set; }
    }
    public static class ProductsWithMultiplePrimaryColumnsTrigger
    {
        [FunctionName("ProductsWithMultiplePrimaryColumnsTrigger")]
        public static void Run(
            [SqlTrigger("[dbo].[ProductsWithMultiplePrimaryColumnsAndIdentity]", ConnectionStringSetting = "SqlConnectionString")]
            IReadOnlyList<SqlChange<MultiplePrimaryKeyProduct>> changes,
            ILogger logger)
        {
            foreach (SqlChange<MultiplePrimaryKeyProduct> change in changes)
            {
                MultiplePrimaryKeyProduct product = change.Item;
                logger.LogInformation($"Change occurred to ProductsWithMultiplePrimaryColumns table row: {change.Operation}");
                logger.LogInformation($"ProductID: {product.ProductID}, ExternalID: {product.ExternalID} Name: {product.Name}, Cost: {product.Cost}");
            }
        }
    }
}