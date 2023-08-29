// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Microsoft.Extensions.Logging;


namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public static class ProductsColumnTypesTrigger
    {
        /// <summary>
        /// Simple trigger function used to verify different column types are serialized correctly.
        /// </summary>
        [FunctionName(nameof(ProductsColumnTypesTrigger))]
        public static void Run(
            [SqlTrigger("[dbo].[ProductsColumnTypes]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<ProductColumnTypes>> changes,
            ILogger logger)
        {
            logger.LogInformation("SQL Changes: " + Utils.JsonSerializeObject(changes));
        }
    }
}