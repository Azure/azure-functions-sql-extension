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
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Telemetry
{
    public sealed class Telemetry
    {
        internal static Telemetry TelemetryInstance = new Telemetry();

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

        public void Initialize(IConfiguration config, ILogger logger)
        {
            this._logger = logger;
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
            // initialize in task to offload to parallel thread
            this._trackEventTask = Task.Factory.StartNew(() => this.InitializeTelemetry(productVersion, config));
            this._initialized = true;
        }

        private void InitializeTelemetry(string productVersion, IConfiguration config)
        {
            try
            {
                var telemetryConfig = new TelemetryConfiguration(InstrumentationKey);
                telemetryConfig.TelemetryInitializers.Add(new TelemetryInitializer());
                this._client = new TelemetryClient(telemetryConfig);
                this._client.Context.Session.Id = CurrentSessionId;
                this._client.Context.Device.OperatingSystem = RuntimeInformation.OSDescription;

                this._commonProperties = new TelemetryCommonProperties(productVersion, this._client, config).GetTelemetryCommonProperties();
                this._commonMeasurements = new Dictionary<string, double>();
            }
            catch (Exception e)
            {
                this._client.TrackException(e);
                this._client = null;
                // we don't want to fail the tool if telemetry fails.
                Debug.Fail(e.ToString());
            }
        }

        public bool Enabled { get; private set; }

        public void TrackEvent(TelemetryEventName eventName, IDictionary<TelemetryPropertyName, string> properties = null,
            IDictionary<TelemetryMeasureName, double> measurements = null)
        {
            try
            {
                if (!this._initialized || !this.Enabled)
                {
                    return;
                }
                this._logger.LogInformation($"Sending event {eventName}");

                //continue task in existing parallel thread
                this._trackEventTask = this._trackEventTask.ContinueWith(
                    x => this.TrackEventTask(eventName.ToString(), properties, measurements)
                );
            }
            catch (Exception ex)
            {
                // We don't want errors sending telemetry to break the app, so just log and move on
                Debug.Fail($"Error sending event {eventName} : {ex.Message}");
            }
        }

        public void TrackException(TelemetryErrorName errorName, Exception exception, IDictionary<TelemetryPropertyName, string> properties = null,
            IDictionary<TelemetryMeasureName, double> measurements = null)
        {
            try
            {
                if (!this._initialized || !this.Enabled)
                {
                    return;
                }
                this._logger.LogInformation($"Sending exception event: {exception.Message}");
                properties = properties != null ? new Dictionary<TelemetryPropertyName, string>(properties) : new Dictionary<TelemetryPropertyName, string>();
                properties[TelemetryPropertyName.ErrorName] = errorName.ToString();
                properties[TelemetryPropertyName.ErrorCode] = ExtractErrorCode(exception);
                //continue task in existing parallel thread
                this._trackEventTask = this._trackEventTask.ContinueWith(
                    x => this.TrackExceptionTask(exception, properties, measurements)
                );
            }
            catch (Exception ex)
            {
                // We don't want errors sending telemetry to break the app, so just log and move on
                Debug.Fail($"Error sending exception event : {ex.Message}");
            }
        }

        /// <summary>
        /// Sends an event with the specified duration added as a measurement
        /// </summary>
        /// <param name="eventName">The name of the event</param>
        /// <param name="durationMs">The duration of the event</param>
        /// <param name="properties">Any other properties to send with the event</param>
        /// <param name="measurements">Any other measurements to send with the event</param>
        public void TrackDuration(TelemetryEventName eventName, long durationMs, IDictionary<TelemetryPropertyName, string> properties = null,
            IDictionary<TelemetryMeasureName, double> measurements = null)
        {
            try
            {
                measurements = measurements != null ? new Dictionary<TelemetryMeasureName, double>(measurements) : new Dictionary<TelemetryMeasureName, double>();
                measurements[TelemetryMeasureName.DurationMs] = durationMs;
                this.TrackEvent(eventName, properties, measurements);
            }
            catch (Exception ex)
            {
                // We don't want errors sending telemetry to break the app, so just log and move on
                Debug.Fail($"Error sending event {eventName} : {ex.Message}");
            }
        }

        /// <summary>
        /// Sends an event indicating the creation of a certain type of object. (mostly used to track the different collectors/converters used in the bindings)
        /// </summary>
        /// <param name="type">The type of object being created</param>
        /// <param name="properties">Any other properties to send with the event</param>
        /// <param name="measurements">Any other measurements to send with the event</param>
        public void TrackCreate(CreateType type, IDictionary<TelemetryPropertyName, string> properties = null,
            IDictionary<TelemetryMeasureName, double> measurements = null)
        {
            try
            {
                properties = properties != null ? new Dictionary<TelemetryPropertyName, string>(properties) : new Dictionary<TelemetryPropertyName, string>();
                properties[TelemetryPropertyName.Type] = type.ToString();
                this.TrackEvent(TelemetryEventName.Create, properties, measurements);
            }
            catch (Exception ex)
            {
                // We don't want errors sending telemetry to break the app, so just log and move on
                Debug.Fail($"Error sending event Create : {ex.Message}");
            }
        }

        /// <summary>
        /// Sends an event indicating a call to convert for an Input binding
        /// </summary>
        /// <param name="type">The type of object we're converting to</param>
        /// <param name="properties">Any other properties to send with the event</param>
        /// <param name="measurements">Any other measurements to send with the event</param>
        public void TrackConvert(ConvertType type, IDictionary<TelemetryPropertyName, string> properties = null,
            IDictionary<TelemetryMeasureName, double> measurements = null)
        {
            try
            {
                properties = properties != null ? new Dictionary<TelemetryPropertyName, string>(properties) : new Dictionary<TelemetryPropertyName, string>();
                properties[TelemetryPropertyName.Type] = type.ToString();
                this.TrackEvent(TelemetryEventName.Convert, properties, measurements);
            }
            catch (Exception ex)
            {
                // We don't want errors sending telemetry to break the app, so just log and move on
                Debug.Fail($"Error sending event Create : {ex.Message}");
            }
        }

        private void TrackEventTask(
            string eventName,
            IDictionary<TelemetryPropertyName, string> properties,
            IDictionary<TelemetryMeasureName, double> measurements)
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

        private void TrackExceptionTask(
            Exception exception,
            IDictionary<TelemetryPropertyName, string> properties,
            IDictionary<TelemetryMeasureName, double> measurements)
        {
            if (this._client is null)
            {
                return;
            }

            try
            {
                Dictionary<string, string> eventProperties = this.GetEventProperties(properties);
                Dictionary<string, double> eventMeasurements = this.GetEventMeasures(measurements);

                this._client.TrackException(exception, eventProperties, eventMeasurements);
                this._client.Flush();
            }
            catch (Exception e)
            {
                Debug.Fail(e.ToString());
            }
        }

        private Dictionary<string, double> GetEventMeasures(IDictionary<TelemetryMeasureName, double> measurements)
        {
            var eventMeasurements = new Dictionary<string, double>(this._commonMeasurements);
            if (measurements != null)
            {
                foreach (KeyValuePair<TelemetryMeasureName, double> measurement in measurements)
                {
                    eventMeasurements[measurement.Key.ToString()] = measurement.Value;
                }
            }
            return eventMeasurements;
        }

        private Dictionary<string, string> GetEventProperties(IDictionary<TelemetryPropertyName, string> properties)
        {
            if (properties != null)
            {
                var eventProperties = new Dictionary<string, string>(this._commonProperties);
                foreach (KeyValuePair<TelemetryPropertyName, string> property in properties)
                {
                    eventProperties[property.Key.ToString()] = property.Value;
                }
                return eventProperties;
            }
            else
            {
                return this._commonProperties;
            }
        }

        /// <summary>
        /// Extract error code from known exception types
        /// </summary>
        private static string ExtractErrorCode(Exception ex)
        {
            if (ex != null && ex is SqlException)
            {
                return (ex as SqlException).Number.ToString(CultureInfo.InvariantCulture);
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// Type of object being created
    /// </summary>
    public enum CreateType
    {
        SqlAsyncCollector,
        SqlConverter,
        SqlGenericsConverter
    }

    /// <summary>
    /// The type of conversion being performed by the input binding
    /// </summary>
    public enum ConvertType
    {
        IAsyncEnumerable,
        IEnumerable,
        Json,
        SqlCommand,
        JArray
    }

    /// <summary>
    /// Event names used for telemetry events
    /// </summary>
    public enum TelemetryEventName
    {
        AcquireLeaseEnd,
        AcquireLeaseStart,
        AddAsync,
        Convert,
        Create,
        Error,
        FlushAsync,
        GetCaseSensitivity,
        GetChangesEnd,
        GetChangesStart,
        GetColumnDefinitions,
        GetPrimaryKeys,
        GetScaleStatus,
        GetTableInfoEnd,
        GetTableInfoStart,
        ReleaseLeasesEnd,
        ReleaseLeasesStart,
        RenewLeasesEnd,
        RenewLeasesStart,
        StartListenerEnd,
        StartListenerStart,
        StopListenerEnd,
        StopListenerStart,
        TableInfoCacheHit,
        TableInfoCacheMiss,
        TriggerFunctionEnd,
        TriggerFunctionStart,
        TriggerMonitorStart,
        UpsertEnd,
        UpsertStart,
    }

    /// <summary>
    /// Names used for properties in a telemetry event
    /// </summary>
    public enum TelemetryPropertyName
    {
        ErrorCode,
        ErrorName,
        ExceptionType,
        HasIdentityColumn,
        HasConfiguredBatchSize,
        HasConfiguredPollingInterval,
        LeasesTableName,
        QueryType,
        ScaleRecommendation,
        ServerVersion,
        TriggerMetrics,
        Type,
        UserFunctionId,
        WorkerCount,
    }

    /// <summary>
    /// Names used for measures in a telemetry event
    /// </summary>
    public enum TelemetryMeasureName
    {
        AcquireLeasesDurationMs,
        BatchCount,
        BatchSize,
        CommandDurationMs,
        CreatedSchemaDurationMs,
        CreateGlobalStateTableDurationMs,
        CreateLeasesTableDurationMs,
        DurationMs,
        GetCaseSensitivityDurationMs,
        GetChangesDurationMs,
        GetColumnDefinitionsDurationMs,
        GetPrimaryKeysDurationMs,
        GetUnprocessedChangesDurationMs,
        InsertGlobalStateTableRowDurationMs,
        PollingIntervalMs,
        ReleaseLeasesDurationMs,
        RetryAttemptNumber,
        SetLastSyncVersionDurationMs,
        TransactionDurationMs,
        UnprocessedChangeCount,
        UpdateLastSyncVersionDurationMs,
    }

    /// <summary>
    /// The generic name for an error (indicating where it originated from)
    /// </summary>
    public enum TelemetryErrorName
    {
        ConsumeChangesLoop,
        Convert,
        FlushAsync,
        GetCaseSensitivity,
        GetChanges,
        GetChangesRollback,
        GetColumnDefinitions,
        GetColumnDefinitionsTableDoesNotExist,
        GetPrimaryKeys,
        GetScaleStatus,
        GetUnprocessedChangeCount,
        MissingPrimaryKeys,
        NoPrimaryKeys,
        ProcessChanges,
        PropsNotExistOnTable,
        ReleaseLeases,
        ReleaseLeasesNoRetriesLeft,
        ReleaseLeasesRollback,
        RenewLeases,
        RenewLeasesLoop,
        StartListener,
        Upsert,
        UpsertRollback,
    }
}

