﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    public static class Utils
    {
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
    }
}
