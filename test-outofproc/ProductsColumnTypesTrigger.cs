// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using DotnetIsolatedTests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;

namespace DotnetIsolatedTests
{
    public static class ProductsColumnTypesTrigger
    {
        /// <summary>
        /// Simple trigger function used to verify different column types are serialized correctly.
        /// </summary>
        [Function(nameof(ProductsColumnTypesTrigger))]
        public static void Run(
            [SqlTrigger("[dbo].[ProductsColumnTypes]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<ProductColumnTypes>> changes,
            FunctionContext context)
        {
            ILogger logger = context.GetLogger("ProductsColumnTypesTrigger");
            logger.LogInformation("SQL Changes: " + Utils.JsonSerializeObject(changes));
        }
    }
}