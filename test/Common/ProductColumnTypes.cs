// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    public class ProductColumnTypes
    {
        public int ProductID { get; set; }

        public DateTime Datetime { get; set; }

        public DateTime Datetime2 { get; set; }
    }
}
