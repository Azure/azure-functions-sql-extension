// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using DotnetIsolatedTests.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;

namespace DotnetIsolatedTests
{
    public static class ReservedPrimaryKeyColumnNamesTrigger
    {
        /// <summary>
        /// Used in verification of the error message when the user table contains one or more primary keys with names
        /// conflicting with column names in the leases table.
        /// </summary>
        [Function(nameof(ReservedPrimaryKeyColumnNamesTrigger))]
        public static void Run(
            [SqlTrigger("[dbo].[ProductsWithReservedPrimaryKeyColumnNames]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> products)
        {
            throw new NotImplementedException("Associated test case should fail before the function is invoked.");
        }
    }
}
