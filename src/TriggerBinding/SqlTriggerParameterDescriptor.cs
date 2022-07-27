// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    /// <summary>
    /// Trigger parameter descriptor for <see cref="SqlTriggerBinding">.
    /// </summary>
    internal sealed class SqlTriggerParameterDescriptor : TriggerParameterDescriptor
    {
        /// <summary>
        /// Name of the user table.
        /// </summary>
        public string TableName { private get; set; }

        /// <summary>
        /// Returns descriptive reason for why the user function was triggered.
        /// </summary>
        /// <param name="arguments">Collection of function arguments (unused)</param>
        public override string GetTriggerReason(IDictionary<string, string> arguments)
        {
            return $"New change detected on table '{this.TableName}' at {DateTime.UtcNow:o}.";
        }
    }
}