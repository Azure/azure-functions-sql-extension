// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    public static class Utils
    {
        /// <summary>
        /// Default JSON serializer settings to use
        /// </summary>
        private static readonly JsonSerializerSettings _defaultJsonSerializationSettings;

        static Utils()
        {
            _defaultJsonSerializationSettings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver()
            };
        }

        /// <summary>
        /// Gets the specified environment variable and converts it to a boolean.
        /// </summary>
        /// <param name="name">Name of the environment variable</param>
        /// <param name="defaultValue">Value to use if the variable doesn't exist or is unable to be parsed</param>
        /// <returns>True if the variable exists and is set to a value that can be parsed as true, false otherwise</returns>
        public static bool GetEnvironmentVariableAsBool(string name, bool defaultValue = false)
        {
            string str = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(str))
            {
                return defaultValue;
            }

            return str.AsBool(defaultValue);
        }

        /// <summary>
        /// Gets the specified configuration setting and converts it to a boolean.
        /// </summary>
        /// <param name="name">Key name of the setting</param>
        /// <param name="config">The config option to retrieve the value from</param>
        /// <param name="defaultValue">Value to use if the setting doesn't exist or is unable to be parsed</param>
        /// <returns>True if the setting exists and is set to a value that can be parsed as true, false otherwise</returns>
        public static bool GetConfigSettingAsBool(string name, IConfiguration config, bool defaultValue = false)
        {
            return config.GetValue(name, defaultValue.ToString()).AsBool(defaultValue);
        }

        /// <summary>
        /// Converts the string into an equivalent boolean value. This is used instead of Convert.ToBool since that
        /// doesn't handle converting the string value "1".
        /// </summary>
        /// <param name="str">The string to convert</param>
        /// <param name="defaultValue">Value to use if the string is unable to be converted, default is false</param>
        /// <returns></returns>
        private static bool AsBool(this string str, bool defaultValue = false)
        {
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

        public static void LogDebugWithThreadId(this ILogger logger, string message, params object[] args)
        {
            logger.LogDebug($"TID:{Environment.CurrentManagedThreadId} {message}", args);
        }

        public static void LogInformationWithThreadId(this ILogger logger, string message, params object[] args)
        {
            logger.LogInformation($"TID:{Environment.CurrentManagedThreadId} {message}", args);
        }

        /// <summary>
        /// Serializes the specified object into a JSON string.
        /// </summary>
        /// <param name="obj">The object to serialize</param>
        /// <param name="settings">The specific settings to use, uses a simple set of default settings if not specified</param>
        /// <returns>The serialized JSON string</returns>
        /// <remarks>This will NOT use any global settings to avoid picking up changes that may have been made by other code running in the host (such as user functions)</remarks>
        public static string JsonSerializeObject(object obj, JsonSerializerSettings settings = null)
        {
            settings = settings ?? _defaultJsonSerializationSettings;
            // Following the Newtonsoft implementation in JsonConvert of creating a new JsonSerializer each time.
            // https://github.com/JamesNK/Newtonsoft.Json/blob/57025815e564d36821acf778e2c00d02225aab35/Src/Newtonsoft.Json/JsonConvert.cs#L612
            // If performance ends up being an issue could look into creating a single instance of the serializer for each setting.
            var serializer = JsonSerializer.Create(settings);
            // 256 is value used by Newtonsoft by default - helps avoid having to expand it too many times for larger strings
            // https://github.com/JamesNK/Newtonsoft.Json/blob/57025815e564d36821acf778e2c00d02225aab35/Src/Newtonsoft.Json/JsonConvert.cs#L659
            var sb = new StringBuilder(256);
            var sw = new StringWriter(sb);
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, obj);
                return sb.ToString();
            }
        }

        /// <summary>
        /// Deserializes the JSON string into an instance of the specified type
        /// </summary>
        /// <typeparam name="T">The type to deserialize into</typeparam>
        /// <param name="json">The string containing the JSON</param>
        /// <param name="settings">The specific settings to use, uses a simple set of default settings if not specified</param>
        /// <returns>The instance of T being deserialized</returns>
        /// <remarks>This will NOT use any global settings to avoid picking up changes that may have been made by other code running in the host (such as user functions)</remarks>
        public static T JsonDeserializeObject<T>(string json, JsonSerializerSettings settings = null)
        {
            settings = settings ?? _defaultJsonSerializationSettings;
            // Following the Newtonsoft implementation in JsonConvert of creating a new JsonSerializer each time.
            // https://github.com/JamesNK/Newtonsoft.Json/blob/57025815e564d36821acf778e2c00d02225aab35/Src/Newtonsoft.Json/JsonConvert.cs#L821
            // If performance ends up being an issue could look into creating a single instance of the serializer for each setting.
            var serializer = JsonSerializer.Create(settings);
            using (JsonReader reader = new JsonTextReader(new StringReader(json)))
            {
                return serializer.Deserialize<T>(reader);
            }
        }
    }
}
