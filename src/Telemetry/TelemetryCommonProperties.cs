// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry
{
    public class TelemetryCommonProperties
    {
        private readonly string _productVersion;
        private readonly string _azureFunctionsEnvironment;
        private readonly bool _hasWebsiteInstanceId;

        public TelemetryCommonProperties(string productVersion, TelemetryClient telemetryClient, IConfiguration config)
        {
            this._productVersion = productVersion;
            this._userLevelCacheWriter = new UserLevelCacheWriter(telemetryClient);
            this._azureFunctionsEnvironment = config.GetValue(AZURE_FUNCTIONS_ENVIRONMENT_KEY, "");
            this._hasWebsiteInstanceId = config.GetValue(WEBSITE_INSTANCE_ID_KEY, "") != "";
        }

        private readonly UserLevelCacheWriter _userLevelCacheWriter;

        private const string OSVersion = "OSVersion";
        private const string ProductVersion = "ProductVersion";
        private const string MachineId = "MachineId";
        private const string AzureFunctionsEnvironment = "AzureFunctionsEnvironment";
        private const string HasWebsiteInstanceId = "HasWebsiteInstanceId";

        private const string AZURE_FUNCTIONS_ENVIRONMENT_KEY = "AZURE_FUNCTIONS_ENVIRONMENT";
        private const string WEBSITE_INSTANCE_ID_KEY = "WEBSITE_INSTANCE_ID";

        public Dictionary<string, string> GetTelemetryCommonProperties()
        {
            return new Dictionary<string, string>
            {
                {OSVersion, RuntimeInformation.OSDescription},
                {ProductVersion, this._productVersion},
                {MachineId, this.GetMachineId()},
                {AzureFunctionsEnvironment, this._azureFunctionsEnvironment},
                {HasWebsiteInstanceId, this._hasWebsiteInstanceId.ToString()}
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

