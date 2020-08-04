using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class ChangeTableData
    {
        // Change this to actually have fields for each of the columns
        public List<Dictionary<string, string>> workerTableRows { get; set; }

        public Dictionary<Dictionary<string, string>, string> whereChecks { get; set; }
    }
}
