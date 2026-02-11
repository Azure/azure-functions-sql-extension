// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlTriggerConstantsTests
    {
        /// <summary>
        /// Verifies that the global app lock statements use the correct resource name and contain
        /// the expected sp_getapplock call with Exclusive mode.
        /// </summary>
        [Fact]
        public void GlobalAppLockStatements_ContainsCorrectResource()
        {
            string statements = SqlTriggerConstants.GlobalAppLockStatements;
            Assert.Contains($"@Resource = '{SqlTriggerConstants.GlobalAppLockResource}'", statements);
            Assert.Contains("@LockMode = 'Exclusive'", statements);
            Assert.Contains($"@LockTimeout = {SqlTriggerConstants.AppLockTimeoutMs}", statements);
            Assert.Contains("sp_getapplock", statements);
            Assert.Contains("RAISERROR", statements);
        }

        /// <summary>
        /// Verifies that GetTableScopedAppLockStatements generates a lock resource name
        /// that includes the table ID with the correct prefix.
        /// </summary>
        [Theory]
        [InlineData(12345)]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(1845581613)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void GetTableScopedAppLockStatements_ContainsTableIdInResource(int userTableId)
        {
            string statements = SqlTriggerConstants.GetTableScopedAppLockStatements(userTableId);
            string expectedResource = $"{SqlTriggerConstants.TableAppLockResourcePrefix}{userTableId}";

            Assert.Contains($"@Resource = '{expectedResource}'", statements);
            Assert.Contains("@LockMode = 'Exclusive'", statements);
            Assert.Contains($"@LockTimeout = {SqlTriggerConstants.AppLockTimeoutMs}", statements);
            Assert.Contains("sp_getapplock", statements);
            Assert.Contains($"Unable to acquire exclusive lock on {expectedResource}", statements);
        }

        /// <summary>
        /// Verifies that different table IDs produce different lock resource names,
        /// ensuring functions monitoring different tables won't block each other.
        /// </summary>
        [Fact]
        public void GetTableScopedAppLockStatements_DifferentTableIds_ProduceDifferentResources()
        {
            string statements1 = SqlTriggerConstants.GetTableScopedAppLockStatements(100);
            string statements2 = SqlTriggerConstants.GetTableScopedAppLockStatements(200);

            Assert.NotEqual(statements1, statements2);
            Assert.Contains("_az_func_TT_100", statements1);
            Assert.Contains("_az_func_TT_200", statements2);
        }

        /// <summary>
        /// Verifies that the same table ID always produces identical lock statements (deterministic).
        /// </summary>
        [Fact]
        public void GetTableScopedAppLockStatements_SameTableId_ProducesIdenticalOutput()
        {
            string statements1 = SqlTriggerConstants.GetTableScopedAppLockStatements(42);
            string statements2 = SqlTriggerConstants.GetTableScopedAppLockStatements(42);

            Assert.Equal(statements1, statements2);
        }

        /// <summary>
        /// Verifies that table-scoped lock uses a different resource name than the global lock,
        /// ensuring runtime operations don't interfere with startup DDL operations.
        /// </summary>
        [Fact]
        public void GetTableScopedAppLockStatements_ResourceDiffersFromGlobalLock()
        {
            string tableScopedStatements = SqlTriggerConstants.GetTableScopedAppLockStatements(12345);

            // Table-scoped should NOT contain the global resource
            Assert.DoesNotContain($"@Resource = '{SqlTriggerConstants.GlobalAppLockResource}'", tableScopedStatements);

            // Global should NOT contain the table-scoped prefix
            Assert.DoesNotContain(SqlTriggerConstants.TableAppLockResourcePrefix, SqlTriggerConstants.GlobalAppLockStatements);
        }
    }
}
