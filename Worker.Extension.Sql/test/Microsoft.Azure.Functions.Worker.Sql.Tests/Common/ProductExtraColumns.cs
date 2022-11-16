﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.Functions.Worker.Sql.Tests.Common
{
    public class ProductExtraColumns
    {
        public int ProductID { get; set; }

        public string? Name { get; set; }

        public int Cost { get; set; }

        public int ExtraInt { get; set; }

        public string? ExtraString { get; set; }
    }

    public class ProductIncludeIdentity
    {
        public int ProductID { get; set; }

        public string? Name { get; set; }

        public int Cost { get; set; }
    }
}