// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlTriggerUtilsTests
    {
        [Fact]
        public async Task RunStartupPhaseAsync_RunsActionAndLogsPhase()
        {
            (Mock<ILogger> mockLogger, List<string> logMessages, List<Exception> logExceptions) = CreateMockLogger();
            bool actionRun = false;

            await SqlTriggerUtils.RunStartupPhaseAsync("TestPhase", "dbo.Products", "testFunctionId", mockLogger.Object, () =>
            {
                actionRun = true;
                return Task.CompletedTask;
            });

            Assert.True(actionRun);
            Assert.Equal(2, logMessages.Count);
            Assert.Equal("SQL trigger startup phase 'TestPhase' started for table: 'dbo.Products', function ID: 'testFunctionId'.", logMessages[0]);
            Assert.StartsWith("SQL trigger startup phase 'TestPhase' completed for table: 'dbo.Products', function ID: 'testFunctionId' in ", logMessages[1]);
            Assert.EndsWith("ms.", logMessages[1]);
            Assert.All(logExceptions, Assert.Null);
        }

        [Fact]
        public async Task RunStartupPhaseAsync_LogsPhaseFailureAndRethrows()
        {
            (Mock<ILogger> mockLogger, List<string> logMessages, List<Exception> logExceptions) = CreateMockLogger();
            var expectedException = new InvalidOperationException("Test failure");

            InvalidOperationException actualException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                SqlTriggerUtils.RunStartupPhaseAsync("FailingPhase", "dbo.Products", "testFunctionId", mockLogger.Object, () => throw expectedException));

            Assert.Same(expectedException, actualException);
            Assert.Equal(2, logMessages.Count);
            Assert.Equal("SQL trigger startup phase 'FailingPhase' started for table: 'dbo.Products', function ID: 'testFunctionId'.", logMessages[0]);
            Assert.StartsWith("SQL trigger startup phase 'FailingPhase' failed for table: 'dbo.Products', function ID: 'testFunctionId' after ", logMessages[1]);
            Assert.Null(logExceptions[0]);
            Assert.Same(expectedException, logExceptions[1]);
        }

        private static (Mock<ILogger> logger, List<string> logMessages, List<Exception> logExceptions) CreateMockLogger()
        {
            var logMessages = new List<string>();
            var logExceptions = new List<Exception>();
            var mockLogger = new Mock<ILogger>();
            mockLogger
                .Setup(logger => logger.Log(It.IsAny<LogLevel>(), 0, It.IsAny<FormattedLogValues>(), It.IsAny<Exception>(), It.IsAny<Func<object, Exception, string>>()))
                .Callback((LogLevel logLevel, EventId eventId, object state, Exception exception, Func<object, Exception, string> formatter) =>
                {
                    logMessages.Add(state.ToString());
                    logExceptions.Add(exception);
                });

            return (mockLogger, logMessages, logExceptions);
        }
        [Theory]
        [InlineData("int", 4, 10, 0, "[int]")]
        [InlineData("bigint", 8, 19, 0, "[bigint]")]
        [InlineData("varchar", 20, 0, 0, "[varchar](20)")]
        [InlineData("VARCHAR", 20, 0, 0, "[VARCHAR](20)")]
        [InlineData("varchar", -1, 0, 0, "[varchar](max)")]
        [InlineData("nvarchar", 40, 0, 0, "[nvarchar](40)")]
        [InlineData("nvarchar", -1, 0, 0, "[nvarchar](max)")]
        [InlineData("nchar", 40, 0, 0, "[nchar](40)")]
        [InlineData("char", 20, 0, 0, "[char](20)")]
        [InlineData("binary", 20, 0, 0, "[binary](20)")]
        [InlineData("varbinary", 20, 0, 0, "[varbinary](20)")]
        [InlineData("varbinary", -1, 0, 0, "[varbinary](max)")]
        [InlineData("numeric", 9, 18, 4, "[numeric](18,4)")]
        [InlineData("decimal", 9, 18, 4, "[decimal](18,4)")]
        [InlineData("NUMERIC", 9, 18, 4, "[NUMERIC]")]
        [InlineData("DECIMAL", 9, 18, 4, "[DECIMAL]")]
        [InlineData("datetime2", 8, 27, 7, "[datetime2]")]
        [InlineData("AliasType", 9, 18, 4, "[AliasType]")]
        [InlineData("Alias Type", 4, 10, 0, "[Alias Type]")]
        [InlineData("Alias]Type", 4, 10, 0, "[Alias]]Type]")]
        public void PrimaryKeyColumnTypeIsQuotedBeforeAddingFacets(string typeName, short length, byte precision, byte scale, string expected)
        {
            Assert.Equal(expected, SqlTriggerUtils.GetPrimaryKeyColumnType(typeName, length, precision, scale));
        }

        [Theory]
        [InlineData("int); INSERT dbo.AliasRenameProbe VALUES(1); CREATE TABLE dbo.AliasRenameRest(ProductId int")]
        [InlineData("int]); INSERT dbo.AliasRenameProbe VALUES(1);--")]
        [InlineData("Alias'Type")]
        [InlineData("Alias.Type")]
        [InlineData("Alias[Type]")]
        public void RenamedAliasTypeCannotAddSqlStatements(string typeName)
        {
            string type = SqlTriggerUtils.GetPrimaryKeyColumnType(typeName, 4, 10, 0);
            string query = $"CREATE TABLE [Leases] ([ProductId] {type}, [Version] bigint, PRIMARY KEY ([ProductId]));";
            var parser = new TSql150Parser(true);
            using var reader = new StringReader(query);
            TSqlScript script = Assert.IsType<TSqlScript>(parser.Parse(reader, out IList<ParseError> errors));

            Assert.Empty(errors);
            CreateTableStatement statement = Assert.IsType<CreateTableStatement>(Assert.Single(Assert.Single(script.Batches).Statements));
            UserDataTypeReference aliasType = Assert.IsType<UserDataTypeReference>(statement.Definition.ColumnDefinitions[0].DataType);
            Assert.Equal(typeName, Assert.Single(aliasType.Name.Identifiers).Value);
        }
    }
}
