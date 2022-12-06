// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal sealed class SqlTriggerMetrics : ScaleMetrics
    {
        /// <summary>
        /// The number of row changes in the user table that are not yet processed.
        /// </summary>
        public long UnprocessedChangeCount { get; set; }
    }
}