// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class UtilsTests
    {
        private const string TestEnvVar = "AzureFunctionsSqlBindingsTestEnvVar";
        private const string TestConfigSetting = "AzureFunctionsSqlBindingsTestConfigSetting";

        [Theory]
        [InlineData(null, false)] // Doesn't exist, get default value
        [InlineData(null, true, true)] // Doesn't exist, get default value (set explicitly)
        [InlineData("1", true)]
        [InlineData("true", true)]
        [InlineData("TRUE", true)]
        [InlineData("yes", true)]
        [InlineData("YES", true)]
        [InlineData("0", false)]
        [InlineData("false", false)]
        [InlineData("FALSE", false)]
        [InlineData("no", false)]
        [InlineData("NO", false)]
        [InlineData("2", false)]
        [InlineData("SomeOtherValue", false)]
        public void GetEnvironmentVariableAsBool(string value, bool expectedValue, bool defaultValue = false)
        {
            Environment.SetEnvironmentVariable(TestEnvVar, value?.ToString());
            bool actualValue = Utils.GetEnvironmentVariableAsBool(TestEnvVar, defaultValue);
            Assert.Equal(expectedValue, actualValue);
        }

        [Theory]
        [InlineData(null, false)] // Doesn't exist, get default value
        [InlineData(null, true, true)] // Doesn't exist, get default value (set explicitly)
        [InlineData("1", true)]
        [InlineData("true", true)]
        [InlineData("TRUE", true)]
        [InlineData("yes", true)]
        [InlineData("YES", true)]
        [InlineData("0", false)]
        [InlineData("false", false)]
        [InlineData("FALSE", false)]
        [InlineData("no", false)]
        [InlineData("NO", false)]
        [InlineData("2", false)]
        [InlineData("SomeOtherValue", false)]
        public void GetConfigSettingAsBool(string value, bool expectedValue, bool defaultValue = false)
        {
            var config = new TestConfiguration();
            IConfigurationSection configSection = new TestConfigurationSection();
            configSection.Value = value;
            config.AddSection(TestConfigSetting, configSection);
            bool actualValue = Utils.GetConfigSettingAsBool(TestConfigSetting, config, defaultValue);
            Assert.Equal(expectedValue, actualValue);
        }
    }
}
