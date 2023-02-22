// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common
{
    public class Product
    {
        public int ProductId { get; set; }

        public string Name { get; set; }

        public int Cost { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is Product)
            {
                var that = obj as Product;
                return this.ProductId == that.ProductId && this.Name == that.Name && this.Cost == that.Cost;
            }
            return false;
        }
    }

    public class ProductWithOptionalId
    {
        public int? ProductId { get; set; }

        public string Name { get; set; }

        public int Cost { get; set; }
    }

    public class ProductName
    {
        public string Name { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is Product)
            {
                var that = obj as Product;
                return this.Name == that.Name;
            }
            return false;
        }
    }

    public class ProductWithDefaultPK
    {
        public string Name { get; set; }

        public int Cost { get; set; }
    }
}