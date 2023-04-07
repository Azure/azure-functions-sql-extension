// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Integration
{
    [CollectionDefinition(Name)]
    public class SqlInputOutputBindingIntegrationTestsCollection : ICollectionFixture<SqlInputOutputBindingIntegrationTestFixture>
    {
        public const string Name = "SqlInputOutputBindingIntegrationTests";

        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}