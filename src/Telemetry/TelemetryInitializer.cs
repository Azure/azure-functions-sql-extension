// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry
{
    public class TelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            // Filter out the Cloud RoleInstance - we don't care about that field and
            // it can contain information such as the machine and domain name of the client
            telemetry.Context.Cloud.RoleInstance = "-";
        }
    }

}
