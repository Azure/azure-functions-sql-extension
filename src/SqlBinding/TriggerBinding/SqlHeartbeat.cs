// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace SqlBinding.TriggerBinding
{
    public class SqlHeartbeat
    {
        internal SqlHeartbeat(long newChanges, long processedChanges, ScaleRecommendation scaleRecommendation)
        {
            this.NewChanges = newChanges;
            this.ProcessedChanges = processedChanges;
            this.ScaleRecommendation = scaleRecommendation;
        }

        public long ProcessedChanges { get; }

        public long NewChanges { get; }

        public ScaleRecommendation ScaleRecommendation { get; }
    }
}
