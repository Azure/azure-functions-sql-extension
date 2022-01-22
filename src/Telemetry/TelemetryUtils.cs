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
        public static void AddConnectionProps(this Dictionary<string, string> props, SqlConnection conn)
        {
            props.Add(nameof(SqlConnection.ServerVersion), conn.ServerVersion);
        }
    }
}