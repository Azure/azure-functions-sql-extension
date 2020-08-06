// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    // Represents the intermediate form of change table data used to read the relevant rows from the user's table
    internal class ChangeTableData
    {
        /// <summary>
        /// A list of rows combining information from the worker table and change table
        /// Each row corresponds to a given primary key value, and contains all columns associated with that value from both tables
        /// The rows are represented by dictionaries whose keys are the column names and values the values of those columns
        /// </summary>
        public List<Dictionary<string, string>> WorkerTableRows { get; set; }

        /// <summary>
        /// Used to build up the queries to extract data from the user table
        /// We want to read rows from the user table corresponding to the primary keys of each row in the workerTableRow
        /// The whereChecks have the appropriate statements that we insert into the WHERE clause of the query
        /// I.e. for an Employee row with primary key values EmployeeID = 3 and Company = 'Microsoft', the 
        /// whereCheck would look like "EmployeeID = 3 AND Company = 'Microsoft'
        /// The keys of the dictionary are the workerTableRows, and the values that row's where check
        /// </summary>
        public Dictionary<Dictionary<string, string>, string> WhereChecks { get; set; }

        /// <summary>
        /// Used in the case that a row was deleted from the user table. In that case, we cannot read the 
        /// row from the user table any longer, so the best we can do is return a row populated only with the
        /// primary key values of the deleted row. This field is used to determine which columns are the primary keys
        /// </summary>
        public Dictionary<string, string> PrimaryKeys { get; set; }
    }
}
