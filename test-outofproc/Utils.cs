// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DotnetIsolatedTests
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
        /// Serializes the specified object into a JSON string.
        /// </summary>
        /// <param name="obj">The object to serialize</param>
        /// <param name="settings">The specific settings to use, uses a simple set of default settings if not specified</param>
        /// <returns>The serialized JSON string</returns>
        /// <remarks>This will NOT use any global settings to avoid picking up changes that may have been made by other code running in the host (such as user functions)</remarks>
        public static string JsonSerializeObject(object obj, JsonSerializerSettings settings = null)
        {
            settings ??= _defaultJsonSerializationSettings;
            // Following the Newtonsoft implementation in JsonConvert of creating a new JsonSerializer each time.
            // https://github.com/JamesNK/Newtonsoft.Json/blob/57025815e564d36821acf778e2c00d02225aab35/Src/Newtonsoft.Json/JsonConvert.cs#L612
            // If performance ends up being an issue could look into creating a single instance of the serializer for each setting.
            var serializer = JsonSerializer.Create(settings);
            // 256 is value used by Newtonsoft by default - helps avoid having to expand it too many times for larger strings
            // https://github.com/JamesNK/Newtonsoft.Json/blob/57025815e564d36821acf778e2c00d02225aab35/Src/Newtonsoft.Json/JsonConvert.cs#L659
            var sb = new StringBuilder(256);
            var sw = new StringWriter(sb);
            using JsonWriter writer = new JsonTextWriter(sw);
            serializer.Serialize(writer, obj);
            return sb.ToString();
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
            settings ??= _defaultJsonSerializationSettings;
            // Following the Newtonsoft implementation in JsonConvert of creating a new JsonSerializer each time.
            // https://github.com/JamesNK/Newtonsoft.Json/blob/57025815e564d36821acf778e2c00d02225aab35/Src/Newtonsoft.Json/JsonConvert.cs#L821
            // If performance ends up being an issue could look into creating a single instance of the serializer for each setting.
            var serializer = JsonSerializer.Create(settings);
            using JsonReader reader = new JsonTextReader(new StringReader(json));
            return serializer.Deserialize<T>(reader);
        }
    }
}
