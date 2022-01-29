// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.ApplicationInsights;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry
{
    public sealed class UserLevelCacheWriter
    {
        private const string AzureFunctionsSqlBindingsProfileDirectoryName = ".azurefunctions-sqlbindings";
        private readonly string _azureFunctionsSqlBindingsTryUserProfileFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AzureFunctionsSqlBindingsProfileDirectoryName);
        private readonly TelemetryClient _telemetryClient;

        public UserLevelCacheWriter(TelemetryClient telemetryClient)
        {
            this._telemetryClient = telemetryClient;
        }

        public string RunWithCache(string cacheKey, Func<string> getValueToCache)
        {
            string cacheFilepath = this.GetCacheFilePath(cacheKey);
            try
            {
                if (!File.Exists(cacheFilepath))
                {
                    if (!Directory.Exists(this._azureFunctionsSqlBindingsTryUserProfileFolderPath))
                    {
                        Directory.CreateDirectory(this._azureFunctionsSqlBindingsTryUserProfileFolderPath);
                    }

                    string runResult = getValueToCache();

                    File.WriteAllText(cacheFilepath, runResult);
                    return runResult;
                }
                else
                {
                    return File.ReadAllText(cacheFilepath);
                }
            }
            catch (Exception ex)
            {
                if (ex is UnauthorizedAccessException
                    || ex is PathTooLongException
                    || ex is IOException)
                {
                    this._telemetryClient.TrackException(ex);
                    return getValueToCache();
                }

                throw;
            }
        }
        private string GetCacheFilePath(string cacheKey)
        {
            return Path.Combine(this._azureFunctionsSqlBindingsTryUserProfileFolderPath, $"{cacheKey}.azureFunctionsSqlBindingsTryUserLevelCache");
        }
    }
}
