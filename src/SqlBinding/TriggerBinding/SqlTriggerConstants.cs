// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal static class SqlTriggerConstants
    {
        public const string PrimaryKeysSelectList = "primaryKeysSelectList";

        public const string UserTableColumnsSelectList = "userTableColumnsSelectList";

        public const string LeftOuterJoinUserTable = "leftOuterJoinUserTable";

        public const string LeftOuterJoinWorkerTable = "leftOuterJoinWorkerTable";

        // Unit of time is seconds
        public const string LeaseUnits = "s";

        public const string ScaleControllerPollingUnits = "s";

        public const string Schema = "az_func";

        public const string GlobalStateTable = "Global_State_Table";

        public const string WorkerBatchSizesTable = "Worker_Batch_Sizes";

        public const long LastChangesDefaultValue = -1;

        public const int BatchSize = 10;

        public const int MaxDequeueCount = 5;

        public const int LeaseInterval = 30;

        public const int PollingInterval = 10;

        public const int CleanupInterval = 300;

        // Unit of time is seconds
        public const string CleanupUnits = "s";

        public const int MaxLeaseRenewalCount = 5;

    }
}