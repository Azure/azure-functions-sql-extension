// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    internal class TestConfigurationSection : IConfigurationSection
    {
        string IConfiguration.this[string key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        string IConfigurationSection.Key => throw new NotImplementedException();

        string IConfigurationSection.Path => throw new NotImplementedException();

        string IConfigurationSection.Value { get; set; }

        IEnumerable<IConfigurationSection> IConfiguration.GetChildren()
        {
            throw new NotImplementedException();
        }

        IChangeToken IConfiguration.GetReloadToken()
        {
            throw new NotImplementedException();
        }

        IConfigurationSection IConfiguration.GetSection(string key)
        {
            throw new NotImplementedException();
        }
    }
}
