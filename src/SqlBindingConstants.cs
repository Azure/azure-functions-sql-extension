// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal static class SqlBindingConstants
    {
        public const string ISO_8061_DATETIME_FORMAT = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffZ";

        /// <summary>
        /// Sql Server Edition of the target server, list consolidated from
        /// https://learn.microsoft.com/en-us/sql/t-sql/functions/serverproperty-transact-sql?view=sql-server-ver16
        /// </summary>
        public enum EngineEdition
        {
            DesktopEngine,
            Standard,
            Enterprise,
            Express,
            SQLDatabase,
            AzureSynapseAnalytics,
            AzureSQLManagedInstance,
            AzureSQLEdge,
            AzureSynapseserverlessSQLpool,
        }
    }
}