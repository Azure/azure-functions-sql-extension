// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common
{
    internal sealed class TestConfiguration : IConfiguration
    {
        private readonly IDictionary<string, IConfigurationSection> _sections = new Dictionary<string, IConfigurationSection>();
        public void AddSection(string key, IConfigurationSection section)
        {
            this._sections[key] = section;
        }

        string IConfiguration.this[string key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

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
            return this._sections[key];
        }
    }
}
