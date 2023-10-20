// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using Microsoft.Azure.WebJobs.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    public class SqlOptions : IOptionsFormatter
    {
        public const int DefaultBatchSize = 100;
        public const int DefaultPollingIntervalMs = 1000;
        private const int DefaultMinimumPollingIntervalMs = 100;

        // NOTE: please ensure the Readme file and other public documentation are also updated if this value ever
        // needs to be changed.
        public const int DefaultMaxChangesPerWorker = 1000;
        /// <summary>
        /// Maximum number of changes to process in each iteration of the loop
        /// </summary>
        private int _batchSize = DefaultBatchSize;
        /// <summary>
        /// Delay in ms between processing each batch of changes
        /// </summary>
        private int _pollingInterval = DefaultPollingIntervalMs;
        private readonly int _minPollingInterval = DefaultMinimumPollingIntervalMs;
        private int _maxChangesPerWorker = DefaultMaxChangesPerWorker;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlOptions"/> class.
        /// </summary>
        public SqlOptions()
        {

        }

        /// <summary>
        /// Gets or sets the number of changes per batch to retrieve from the server.
        /// The default is 100.
        /// </summary>
        public int BatchSize
        {
            get => this._batchSize;

            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                this._batchSize = value;
            }
        }

        /// <summary>
        /// Gets or sets the longest period of time to wait before checking for next batch of changes on the server.
        /// </summary>
        public int PollingIntervalMs
        {
            get => this._pollingInterval;

            set
            {
                if (value < this._minPollingInterval)
                {
                    string message = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                        "PollingInterval must not be less than {0}Ms.", this._minPollingInterval);
                    throw new ArgumentException(message, nameof(value));
                }

                this._pollingInterval = value;
            }
        }

        /// <summary>
        /// Gets or sets the upper limit on the number of pending changes in the user table that are allowed per application-worker.
        /// If the count of changes exceeds this limit, it may result in a scale-out. The setting only applies for Azure Function Apps with runtime driven scaling enabled.
        /// </summary>
        public int MaxChangesPerWorker
        {
            get => this._maxChangesPerWorker;

            set
            {
                if (value < 1)
                {
                    throw new ArgumentException("MaxChangesPerWorker must not be less than 1.", nameof(value));
                }

                this._maxChangesPerWorker = value;
            }
        }

        /// <inheritdoc/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        string IOptionsFormatter.Format()
        {
            var options = new JObject
            {
                { nameof(this.BatchSize), this.BatchSize },
                { nameof(this.PollingIntervalMs), this.PollingIntervalMs },
                { nameof(this.MaxChangesPerWorker), this.MaxChangesPerWorker }
            };

            return options.ToString(Formatting.Indented);
        }

        internal SqlOptions Clone()
        {
            var copy = new SqlOptions
            {
                _batchSize = this._batchSize,
                _pollingInterval = this._pollingInterval,
                _maxChangesPerWorker = this._maxChangesPerWorker
            };
            return copy;
        }
    }

}