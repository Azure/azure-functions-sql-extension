// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Protocols;
using System;
using System.Collections.Generic;

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
        /// <param name="arguments">
        /// Stores the table name and time changes were detected
        /// </param>
        /// <returns>A string with the reason</returns>
        public override string GetTriggerReason(IDictionary<string, string> arguments)
        {
            return string.Format("New changes on table {0} at {1}", this.TableName, DateTime.UtcNow.ToString("o"));
        }
    }
}