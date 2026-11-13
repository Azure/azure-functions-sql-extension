// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlTriggerListenerTests
    {
        [Theory]
        [InlineData(null, false)]
        [InlineData(null, true)]
        [InlineData("leases", false)]
        [InlineData("leases", true)]
        [InlineData("O'Brien", false)]
        [InlineData("O'Brien", true)]
        [InlineData("leases]with'quotes", false)]
        [InlineData("leases]with'quotes", true)]
        [InlineData("leases''with.multiple'quotes", false)]
        [InlineData("leases''with.multiple'quotes", true)]
        [InlineData("audit', 'U') IS NULL SELECT 424242; --", false)]
        [InlineData("audit', 'U') IS NULL SELECT 424242; --", true)]
        public void GetCreateLeasesTableQuery_EscapesObjectIdLiterals(string name, bool migrate)
        {
            const string userFunctionId = "0123456789abcdef";
            const string hostIdFunctionId = "fedcba9876543210";
            string rawName = name == null ? null : migrate ? $"{name}_{userFunctionId}" : name;
            string leasesTableName = SqlTriggerUtils.GetBracketedLeasesTableName(rawName, userFunctionId, 42);
            string appLockStatements = SqlTriggerConstants.GetAppLockStatements(60000);
            string query = SqlTriggerListener<object>.GetCreateLeasesTableQuery(
                leasesTableName, new[] { (name: "PK", type: "int") }, userFunctionId,
                migrate ? hostIdFunctionId : userFunctionId, appLockStatements);

            var parser = new TSql150Parser(false);
            TSqlFragment fragment = parser.Parse(new StringReader(query), out IList<ParseError> errors);
            Assert.Empty(errors);

            var visitor = new LeaseTableQueryVisitor();
            fragment.Accept(visitor);

            Assert.Contains(appLockStatements, query);
            Assert.Equal(0, visitor.SelectStatementCount);
            Assert.Equal(rawName ?? $"Leases_{userFunctionId}_42", Assert.Single(visitor.CreateTableNames));
            Assert.Equal(migrate ? 2 : 1, visitor.ObjectIdArguments.Count);
            Assert.Equal(leasesTableName, visitor.ObjectIdArguments[0].Value);
            Assert.All(visitor.ObjectIdArguments, argument => Assert.True(argument.IsNational));

            if (migrate)
            {
                string oldLeasesTableName = leasesTableName.Replace(userFunctionId, hostIdFunctionId);
                Assert.Equal(oldLeasesTableName, visitor.ObjectIdArguments[1].Value);
                Assert.Contains($"SELECT * FROM {oldLeasesTableName}", query);
                Assert.Contains($"DROP TABLE {oldLeasesTableName}", query);
            }
        }

        private sealed class LeaseTableQueryVisitor : TSqlFragmentVisitor
        {
            public List<StringLiteral> ObjectIdArguments { get; } = new List<StringLiteral>();
            public List<string> CreateTableNames { get; } = new List<string>();
            public int SelectStatementCount { get; private set; }

            public override void Visit(FunctionCall node)
            {
                if (node.FunctionName.Value == "OBJECT_ID")
                {
                    this.ObjectIdArguments.Add(Assert.IsType<StringLiteral>(node.Parameters[0]));
                }
            }

            public override void Visit(CreateTableStatement node)
            {
                this.CreateTableNames.Add(node.SchemaObjectName.BaseIdentifier.Value);
            }

            public override void Visit(SelectStatement node)
            {
                this.SelectStatementCount++;
            }
        }
    }
}
