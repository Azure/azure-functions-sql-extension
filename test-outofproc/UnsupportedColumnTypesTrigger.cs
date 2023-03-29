// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using DotnetIsolatedTests.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;

namespace DotnetIsolatedTests
{
    public static class UnsupportedColumnTypesTrigger
    {
        /// <summary>
        /// Used in verification of the error message when the user table contains columns of unsupported SQL types.
        /// </summary>
        [Function(nameof(UnsupportedColumnTypesTrigger))]
        public static void Run(
            [SqlTrigger("[dbo].[ProductsWithUnsupportedColumnTypes]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> products)
        {
            throw new NotImplementedException("Associated test case should fail before the function is invoked.");
        }
    }
}
