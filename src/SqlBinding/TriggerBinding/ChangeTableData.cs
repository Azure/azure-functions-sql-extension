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
        /// Has the form "PrimaryKey1 = @PrimaryKey1, PrimaryKey2 = @PrimaryKey2" where PrimaryKey1 is a primary key column name
        /// </summary>
        public string WhereCheck { get; set; }

        /// <summary>
        /// Used to build up the queries to extract data from the user table
        /// Lists all primary key columns of this table, which can be used to determine which columns 
        /// in the WorkerTableRows are primary keys
        /// </summary>
        public IEnumerable<string> PrimaryKeys { get; set; }
    }
}
