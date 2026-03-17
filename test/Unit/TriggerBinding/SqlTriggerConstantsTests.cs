// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlTriggerConstantsTests
    {
        [Fact]
        public void GetAppLockStatements_ContainsTimeout()
        {
            string statements = SqlTriggerConstants.GetAppLockStatements(30000);
            Assert.Contains("@LockTimeout = 30000", statements);
        }

        [Fact]
        public void GetAppLockStatements_UsesProvidedTimeout()
        {
            string statements = SqlTriggerConstants.GetAppLockStatements(60000);
            Assert.Contains("@LockTimeout = 60000", statements);
            Assert.DoesNotContain("@LockTimeout = 30000", statements);
        }

        [Fact]
        public void GetAppLockStatements_ContainsAppLockResource()
        {
            string statements = SqlTriggerConstants.GetAppLockStatements(30000);
            Assert.Contains(SqlTriggerConstants.AppLockResource, statements);
        }

        [Fact]
        public void GetAppLockStatements_ContainsSpGetAppLock()
        {
            string statements = SqlTriggerConstants.GetAppLockStatements(30000);
            Assert.Contains("sp_getapplock", statements);
            Assert.Contains("@LockMode = 'Exclusive'", statements);
        }

        [Fact]
        public void GetAppLockStatements_ContainsErrorHandling()
        {
            string statements = SqlTriggerConstants.GetAppLockStatements(30000);
            Assert.Contains("RAISERROR", statements);
            Assert.Contains("IF @result < 0", statements);
        }

        [Fact]
        public void DefaultAppLockTimeoutMs_Is30000()
        {
            Assert.Equal(30000, SqlTriggerConstants.DefaultAppLockTimeoutMs);
        }
    }
}
