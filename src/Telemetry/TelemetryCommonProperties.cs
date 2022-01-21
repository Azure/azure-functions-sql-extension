// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry
{
    public class TelemetryCommonProperties
    {
        private readonly string _productVersion;

        public TelemetryCommonProperties(
            string productVersion)
        {
            this._productVersion = productVersion;
        }

        private const string OSVersion = "OSVersion";
        private const string ProductVersion = "ProductVersion";

        public Dictionary<string, string> GetTelemetryCommonProperties()
        {
            return new Dictionary<string, string>
            {
                {OSVersion, RuntimeInformation.OSDescription},
                {ProductVersion, this._productVersion}
            };
        }
    }
}

