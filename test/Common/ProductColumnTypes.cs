// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    public class ProductColumnTypes
    {
        public int ProductId { get; set; }

        public DateTime Datetime { get; set; }

        public DateTime Datetime2 { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is ProductColumnTypes)
            {
                var that = obj as ProductColumnTypes;
                return this.ProductId == that.ProductId && this.Datetime == that.Datetime && this.Datetime2 == that.Datetime2;
            }
            return false;
        }
    }
}
