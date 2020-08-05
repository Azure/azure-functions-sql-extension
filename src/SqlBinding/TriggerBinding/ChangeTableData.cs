// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class ChangeTableData
    {
        // Change this to actually have fields for each of the columns
        public List<Dictionary<string, string>> workerTableRows { get; set; }

        public Dictionary<Dictionary<string, string>, string> whereChecks { get; set; }
    }
}
