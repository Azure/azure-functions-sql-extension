// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public static class UnsupportedColumnTypesTrigger
    {
        [FunctionName(nameof(UnsupportedColumnTypesTrigger))]
        public static void Run(
            [SqlTrigger("[dbo].[ProductsWithUnsupportedColumnTypes]", ConnectionStringSetting = "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> products)
        {
            throw new NotImplementedException();
        }
    }
}
