// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry
{
    public static class TelemetryUtils
    {
        /// <summary>
        /// Adds common connection properties to the property bag for a telemetry event.
        /// </summary>
        /// <param name="props">The property bag to add our connection properties to</param>
        /// <param name="conn">The connection to add properties of</param>
        /// <param name="engineEdition">The Engine Edition of the target Sql Server</param>
        public static void AddConnectionProps(this IDictionary<TelemetryPropertyName, string> props, SqlConnection conn, string engineEdition)
        {
            props.Add(TelemetryPropertyName.ServerVersion, conn.ServerVersion);
            props.Add(TelemetryPropertyName.EngineEdition, string.IsNullOrEmpty(engineEdition) ? "Unknown" : engineEdition);
        }

        /// <summary>
        /// Returns a dictionary with common connection properties for the specified connection.
        /// </summary>
        /// <param name="conn">The connection to get properties of</param>
        /// <param name="engineEdition">The Engine Edition of the target Sql Server</param>
        /// <returns>The property dictionary</returns>
        public static Dictionary<TelemetryPropertyName, string> AsConnectionProps(this SqlConnection conn, string engineEdition)
        {
            var props = new Dictionary<TelemetryPropertyName, string>();
            props.AddConnectionProps(conn, engineEdition);
            return props;
        }
    }
}