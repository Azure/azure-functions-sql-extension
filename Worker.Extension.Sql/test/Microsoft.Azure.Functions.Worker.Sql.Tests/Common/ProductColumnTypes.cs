// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Functions.Worker.Sql.Tests.Common
{
    public class ProductColumnTypes
    {
        public int ProductID { get; set; }

        public DateTime Datetime { get; set; }

        public DateTime Datetime2 { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is not null and ProductColumnTypes)
            {
                var that = obj as ProductColumnTypes;
                return this.ProductID == that?.ProductID && this.Datetime == that.Datetime && this.Datetime2 == that.Datetime2;
            }
            return false;
        }
    }
}
