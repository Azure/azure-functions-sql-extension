// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
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
        public static void AddConnectionProps(this IDictionary<string, string> props, SqlConnection conn)
        {
            props.Add(TelemetryPropertyName.ServerVersion.ToString(), conn.ServerVersion);
        }

        /// <summary>
        /// Adds common exception properties to the property bag for a telemetry event.
        /// </summary>
        /// <param name="props"></param>
        /// <param name="ex">The exception to add properties of</param>
        public static void AddExceptionProps(this IDictionary<string, string> props, Exception ex)
        {
            props.Add(TelemetryPropertyName.ExceptionType.ToString(), ex?.GetType().Name ?? "");
        }

        /// <summary>
        /// Returns a dictionary with common connection properties for the specified connection.
        /// </summary>
        /// <param name="conn">The connection to get properties of</param>
        /// <returns>The property dictionary</returns>
        public static Dictionary<string, string> AsConnectionProps(this SqlConnection conn)
        {
            var props = new Dictionary<string, string>();
            props.AddConnectionProps(conn);
            return props;
        }
    }
}