// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal static class SqlTriggerConstants
    {
        public const string SchemaName = "az_func";
        public const string GlobalStateTableName = "[" + SchemaName + "].[GlobalState]";
        public const string WorkerTableNameFormat = "[" + SchemaName + "].[Worker_{0}]";
    }
}