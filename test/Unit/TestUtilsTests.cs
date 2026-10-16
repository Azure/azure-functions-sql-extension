// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Common;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class TestUtilsTests
    {
        [Fact]
        public void DefaultPortAvoidsFixtureAndAgentPorts()
        {
            const int NetworkIsolationPort = 7081;
            Assert.InRange(TestUtils.DefaultPort, NetworkIsolationPort + 1, ushort.MaxValue);

            foreach (SupportedLanguages language in Enum.GetValues(typeof(SupportedLanguages)))
            {
                Assert.True(TestUtils.DefaultPort > TestUtils.GetPort(language));
                Assert.True(TestUtils.DefaultPort > TestUtils.GetPort(language, testFolder: true));
            }
        }
    }
}
