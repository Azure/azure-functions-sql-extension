// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    public class ProductUnsupportedTypes
    {
        public int ProductId { get; set; }

        public string TextCol { get; set; }

        public string NtextCol { get; set; }

        public byte[] ImageCol { get; set; }
    }
}