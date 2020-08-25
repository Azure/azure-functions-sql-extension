// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal class SqlTriggerParameterDescriptor : TriggerParameterDescriptor
    {
        /// <summary>
        /// Name of the table being monitored
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// The reason the user's function was triggered. Specifies the table name that experienced changes
        /// as well as the time the changes were detected
        /// </summary>
        /// <param name="arguments">Unused</param>
        /// <returns>A string with the reason</returns>
        public override string GetTriggerReason(IDictionary<string, string> arguments)
        {
            return $"New changes on table {TableName} at {DateTime.UtcNow.ToString("o")}";
        }
    }
}