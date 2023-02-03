// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Extensions.Hosting;
using Xunit;
using System;

namespace WorkerUnitTests
{
    public class SqlInputBindingTests
    {
        [Fact]
        public void TestNullCommandText()
        {
            Assert.Throws<ArgumentNullException>(() => new SqlInputAttribute(null));
            Assert.Throws<ArgumentNullException>(() => new SqlOutputAttribute(null));

        }

        [Fact]
        public void TestNullBuilder()
        {
            HostBuilder builder = null;
            Assert.Throws<NullReferenceException>(() => builder.ConfigureFunctionsWorkerDefaults());
        }

    }
}