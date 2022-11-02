// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Samples.Common
{
    public class Product
    {
        public int ProductID { get; set; }

        public string Name { get; set; }

        public int Cost { get; set; }
    }

    public class ProductWithOptionalId
    {
        public int? ProductID { get; set; }

        public string Name { get; set; }

        public int Cost { get; set; }
    }

    public class ProductName
    {
        public string Name { get; set; }

    }

    public class ProductWithDefaultPK
    {
        public string Name { get; set; }

        public int Cost { get; set; }
    }
}