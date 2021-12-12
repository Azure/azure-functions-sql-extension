// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry
{
    public sealed class Telemetry
    {
        internal static Telemetry Instance = new Telemetry(typeof(Telemetry).Assembly.GetName().Version.ToString(), "azure-functions-sql-ext");

        private readonly string _eventsNamespace;
        internal static string CurrentSessionId;
        private TelemetryClient _client;
        private Dictionary<string, string> _commonProperties;
        private Dictionary<string, double> _commonMeasurements;
        private Task _trackEventTask;

        private const string InstrumentationKey = "9f1f76dd-a432-4b93-ba9e-c98336deacb1";
        public const string TelemetryOptout = "AZUREFUNCTIONS_SQLEXT_TELEMETRY_OPTOUT";

        public const string WelcomeMessage = @"Welcome to .NET Interactive!
---------------------
Telemetry
---------
The .NET Core tools collect usage data in order to help us improve your experience.The data is anonymous and doesn't include command-line arguments. The data is collected by Microsoft and shared with the community. You can opt-out of telemetry by setting the DOTNET_INTERACTIVE_CLI_TELEMETRY_OPTOUT environment variable to '1' or 'true' using your favorite shell.
";

        public Telemetry(
            string productVersion,
            string eventsNamespace,
            string sessionId = null,
            bool blockThreadInitialization = false)
        {
            if (string.IsNullOrWhiteSpace(eventsNamespace))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(eventsNamespace));
            }
            this._eventsNamespace = eventsNamespace;
            this.Enabled = !GetEnvironmentVariableAsBool(TelemetryOptout); // && PermissionExists(sentinel);

            if (!this.Enabled)
            {
                return;
            }

            // Store the session ID in a static field so that it can be reused
            CurrentSessionId = sessionId ?? Guid.NewGuid().ToString();

            if (blockThreadInitialization)
            {
                this.InitializeTelemetry(productVersion);
            }
            else
            {
                //initialize in task to offload to parallel thread
                this._trackEventTask = Task.Factory.StartNew(() => this.InitializeTelemetry(productVersion));
            }
        }

        public bool Enabled { get; }

        // public static bool SkipFirstTimeExperience => GetEnvironmentVariableAsBool(FirstTimeUseNoticeSentinel.SkipFirstTimeExperienceEnvironmentVariableName, false);

        private static bool GetEnvironmentVariableAsBool(string name, bool defaultValue = false)
        {
            string str = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(str))
            {
                return defaultValue;
            }

            switch (str.ToLowerInvariant())
            {
                case "true":
                case "1":
                case "yes":
                    return true;
                case "false":
                case "0":
                case "no":
                    return false;
                default:
                    return defaultValue;
            }
        }

        public void TrackEvent(string eventName, IDictionary<string, string> properties,
            IDictionary<string, double> measurements, ILogger logger)
        {
            logger.LogInformation($"Sending event {eventName}");
            if (!this.Enabled)
            {
                return;
            }

            //continue task in existing parallel thread
            this._trackEventTask = this._trackEventTask.ContinueWith(
                x => this.TrackEventTask(eventName, properties, measurements)
            );
        }

        private void InitializeTelemetry(string productVersion)
        {
            try
            {
                var config = new TelemetryConfiguration(InstrumentationKey);
                this._client = new TelemetryClient(config);
                this._client.Context.Session.Id = CurrentSessionId;
                this._client.Context.Device.OperatingSystem = RuntimeInformation.OSDescription;

                this._commonProperties = new TelemetryCommonProperties(productVersion).GetTelemetryCommonProperties();
                this._commonMeasurements = new Dictionary<string, double>();
            }
            catch (Exception e)
            {
                this._client = null;
                // we don't want to fail the tool if telemetry fails.
                Debug.Fail(e.ToString());
            }
        }

        private void TrackEventTask(
            string eventName,
            IDictionary<string, string> properties,
            IDictionary<string, double> measurements)
        {
            if (this._client is null)
            {
                return;
            }

            try
            {
                Dictionary<string, string> eventProperties = this.GetEventProperties(properties);
                Dictionary<string, double> eventMeasurements = this.GetEventMeasures(measurements);

                this._client.TrackEvent($"{this._eventsNamespace}/{eventName}", eventProperties, eventMeasurements);
                this._client.Flush();
            }
            catch (Exception e)
            {
                Debug.Fail(e.ToString());
            }
        }

        private Dictionary<string, double> GetEventMeasures(IDictionary<string, double> measurements)
        {
            var eventMeasurements = new Dictionary<string, double>(this._commonMeasurements);
            if (measurements != null)
            {
                foreach (KeyValuePair<string, double> measurement in measurements)
                {
                    eventMeasurements[measurement.Key] = measurement.Value;
                }
            }
            return eventMeasurements;
        }

        private Dictionary<string, string> GetEventProperties(IDictionary<string, string> properties)
        {
            if (properties != null)
            {
                var eventProperties = new Dictionary<string, string>(this._commonProperties);
                foreach (KeyValuePair<string, string> property in properties)
                {
                    eventProperties[property.Key] = property.Value;
                }
                return eventProperties;
            }
            else
            {
                return this._commonProperties;
            }
        }
    }
}

