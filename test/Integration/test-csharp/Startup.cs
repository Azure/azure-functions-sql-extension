// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration.Startup))]

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            // Set default settings for JsonConvert to simulate a user doing the same in their function.
            // This will cause test failures if serialization/deserialization isn't done correctly
            // (using the helper methods in Utils.cs)
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                // Test code - not security issue
#pragma warning disable CA2327
                TypeNameHandling = TypeNameHandling.Objects,
#pragma warning restore CA2327
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }
    }
}

