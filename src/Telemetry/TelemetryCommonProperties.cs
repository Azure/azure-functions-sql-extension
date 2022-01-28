// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.ApplicationInsights;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry
{
    public class TelemetryCommonProperties
    {
        private readonly string _productVersion;

        public TelemetryCommonProperties(string productVersion, TelemetryClient telemetryClient)
        {
            this._productVersion = productVersion;
            this._userLevelCacheWriter = new UserLevelCacheWriter(telemetryClient);
        }

        private readonly UserLevelCacheWriter _userLevelCacheWriter;

        private const string OSVersion = "OSVersion";
        private const string ProductVersion = "ProductVersion";
        private const string MachineId = "MachineId";

        public Dictionary<string, string> GetTelemetryCommonProperties()
        {
            return new Dictionary<string, string>
            {
                {OSVersion, RuntimeInformation.OSDescription},
                {ProductVersion, this._productVersion},
                {MachineId, this.GetMachineId()},
            };
        }

        private string GetMachineId()
        {
            return this._userLevelCacheWriter.RunWithCache(MachineId, () =>
            {
                // For now just use random GUID. Typically hashed MAC address is used but that's not
                // something we need currently - a GUID per user is sufficient.
                return Guid.NewGuid().ToString();
            });
        }
    }
}

