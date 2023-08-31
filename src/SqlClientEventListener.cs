// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;
using System.Diagnostics.Tracing;
using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{

    /// <summary>
    /// This listener class will listen for events from the SqlClientEventSource class
    /// and forward them to the logger.
    /// </summary>
    public class SqlClientListener : EventListener
    {
        private readonly ILogger _logger;

        public SqlClientListener(ILogger logger)
        {
            this._logger = logger;
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            // Only enable events from SqlClientEventSource.
            if (string.CompareOrdinal(eventSource.Name, "Microsoft.Data.SqlClient.EventSource") == 0)
            {
                // Use EventKeyWord 2 to capture basic application flow events.
                // See https://docs.microsoft.com/sql/connect/ado-net/enable-eventsource-tracing for all available keywords.
                this.EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)2);
            }
        }

        /// <summary>
        /// This callback runs whenever an event is written by SqlClientEventSource.
        /// Event data is accessed through the EventWrittenEventArgs parameter.
        /// </summary>
        /// <param name="eventData">The data for the event</param>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            try
            {
                if (eventData.Payload == null)
                {
                    return;
                }

                foreach (object payload in eventData.Payload)
                {
                    if (payload != null)
                    {
                        this._logger.LogTrace($"EventID {eventData.EventId}. Payload: {payload}");
                    }
                }
            }
            catch (Exception ex)
            {
                this._logger.LogError($"Error logging SqlClient event {eventData.EventName}. Message={ex.Message}");

            }
        }
    }
}
