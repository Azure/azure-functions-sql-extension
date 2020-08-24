// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace SqlBinding.TriggerBinding
{
    /// <summary>
    /// Represents a scale recommendation given the current metrics for changes occurring to a SQL table
    /// </summary>
    public class ScaleRecommendation : EventArgs
    {
        internal ScaleRecommendation(ScaleAction scaleAction, bool keepWorkersAlive, string reason)
        {
            this.Action = scaleAction;
            this.KeepWorkersAlive = keepWorkersAlive;
            this.Reason = reason;
        }

        /// <summary>
        /// Gets the recommended scale action 
        /// </summary>
        public ScaleAction Action { get; }

        /// <summary>
        /// Gets a recommendation about whether to keep existing workers alive
        /// </summary>
        public bool KeepWorkersAlive { get; }

        /// <summary>
        /// Gets text describing why a particular scale action was recommended.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Gets a string description of the current <see cref="ScaleRecommendation"/> object.
        /// </summary>
        /// <returns>A string description useful for diagnostics.</returns>
        public override string ToString()
        {
            return $"{nameof(this.Action)}: {this.Action}, {nameof(KeepWorkersAlive)}: {this.KeepWorkersAlive}, {nameof(Reason)}: {this.Reason}";
        }

        /// <summary>
        /// Possible scale actions
        /// </summary>
        public enum ScaleAction
        {
            /// <summary>
            /// Do not add or remove workers.
            /// </summary>
            None = 0,

            /// <summary>
            /// Add workers to the current task hub.
            /// </summary>
            AddWorker,

            /// <summary>
            /// Remove workers from the current task hub.
            /// </summary>
            RemoveWorker
        }
    }
}
