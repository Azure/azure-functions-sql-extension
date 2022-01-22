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
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.WebJobs.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry
{
    public sealed class Telemetry
    {
        internal static Telemetry Instance = new Telemetry();

        private const string EventsNamespace = "azure-functions-sql-bindings";
        internal static string CurrentSessionId;
        private TelemetryClient _client;
        private Dictionary<string, string> _commonProperties;
        private Dictionary<string, double> _commonMeasurements;
        private Task _trackEventTask;
        private ILogger _logger;
        private bool _initialized;
        private const string InstrumentationKey = "98697a1c-1416-486a-99ac-c6c74ebe5ebd";
        /// <summary>
        /// The environment variable used for opting out of telemetry
        /// </summary>
        public const string TelemetryOptoutEnvVar = "AZUREFUNCTIONS_SQLBINDINGS_TELEMETRY_OPTOUT";
        /// <summary>
        /// The app setting used for opting out of telemetry
        /// </summary>
        public const string TelemetryOptoutSetting = "AzureFunctionsSqlBindingsTelemetryOptOut";

        public const string WelcomeMessage = @"Azure SQL binding for Azure Functions
---------------------
Telemetry
---------
This extension collect usage data in order to help us improve your experience. The data is anonymous and doesn't include any personal information. You can opt-out of telemetry by setting the " + TelemetryOptoutEnvVar + " environment variable or the " + TelemetryOptoutSetting + @" + app setting to '1', 'true' or 'yes';
";

        public void Initialize(IConfiguration config, ILoggerFactory loggerFactory)
        {
            this._logger = loggerFactory.CreateLogger(LogCategories.Bindings);
            this.Enabled = !(Utils.GetEnvironmentVariableAsBool(TelemetryOptoutEnvVar) || Utils.GetConfigSettingAsBool(TelemetryOptoutSetting, config));
            if (!this.Enabled)
            {
                this._logger.LogInformation("Telemetry disabled");
                return;
            }
            this._logger.LogInformation(WelcomeMessage);
            // Store the session ID in a static field so that it can be reused
            CurrentSessionId = Guid.NewGuid().ToString();

            string productVersion = typeof(Telemetry).Assembly.GetName().Version.ToString();
            //initialize in task to offload to parallel thread
            this._trackEventTask = Task.Factory.StartNew(() => this.InitializeTelemetry(productVersion));
            this._initialized = true;
        }

        public bool Enabled { get; private set; }

        public void TrackEvent(string eventName, IDictionary<string, string> properties,
            IDictionary<string, double> measurements)
        {
            if (!this._initialized || !this.Enabled)
            {
                return;
            }
            this._logger.LogInformation($"Sending event {eventName}");

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

                this._client.TrackEvent($"{EventsNamespace}/{eventName}", eventProperties, eventMeasurements);
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

