// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace SqlBinding.TriggerBinding
{
    public class SqlHeartbeat
    {
        internal SqlHeartbeat(long unprocessedChanges, ScaleRecommendation scaleRecommendation)
        {
            this.UnprocessedChanges = unprocessedChanges;
            this.ScaleRecommendation = scaleRecommendation;
        }

        public long UnprocessedChanges { get; }

        public ScaleRecommendation ScaleRecommendation { get; }
    }
}
