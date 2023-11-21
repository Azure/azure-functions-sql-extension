// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public static class PrimaryKeyNotPresentTrigger
    {
        /// <summary>
        /// Used in verification of the error message when the user table does not contain primary key.
        /// </summary>
        [FunctionName(nameof(PrimaryKeyNotPresentTrigger))]
        public static void Run(
            [SqlTrigger("[dbo].[ProductsWithoutPrimaryKey]", "SqlConnectionString")]
            IReadOnlyList<SqlChange<Product>> products)
        {
            throw new NotImplementedException("Associated test case should fail before the function is invoked.");
        }
    }
}
