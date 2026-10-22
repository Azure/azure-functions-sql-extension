// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlScalerProviderTests
    {
        [Theory]
        [InlineData("SqlConnectionString")]
        [InlineData("ConnectionStrings:SqlConnectionString")]
        public void Constructor_WithFactoryOverride_ConsumesCredential(string settingName)
        {
            const string ConnectionString = "Server=server;Database=database;Authentication=Active Directory Managed Identity;User ID=client-id";
            var componentFactory = new TestAzureComponentFactory(new TestTokenCredential());
            TriggerMetadata triggerMetadata = CreateTriggerMetadata();
            triggerMetadata.Properties[nameof(AzureComponentFactory)] = componentFactory;
            using ServiceProvider serviceProvider = CreateServiceProvider(ConnectionString, settingName);

            var provider = new SqlScalerProvider(serviceProvider, triggerMetadata);

            Assert.IsType<SqlTriggerScaleMonitor>(provider.GetMonitor());
            Assert.IsType<SqlTriggerTargetScaler>(provider.GetTargetScaler());
            Assert.Equal(1, componentFactory.CreateTokenCredentialCallCount);
            Assert.Same(serviceProvider.GetService<IConfiguration>(), componentFactory.LastConfiguration);
        }

        [Theory]
        [InlineData("Authentication=Active Directory Managed Identity;User ID=client-id")]
        [InlineData("Authentication=Sql Password;User ID=sql-user;Password=test-password")]
        [InlineData("Authentication=Active Directory Default")]
        public void Constructor_WithoutFactoryOverride_PreservesCurrentConnectionStringBehavior(string authentication)
        {
            string connectionString = $"Server=server;Database=database;{authentication}";
            using ServiceProvider serviceProvider = CreateServiceProvider(connectionString);
            var defaultFactory = new Mock<AzureComponentFactory>(MockBehavior.Strict);
            var services = new ServiceCollection();
            services.AddSingleton(serviceProvider.GetService<IConfiguration>());
            services.AddSingleton(defaultFactory.Object);
            services.AddLogging();
            services.Configure<SqlOptions>(_ => { });
            using ServiceProvider providerWithDefaultFactory = services.BuildServiceProvider();

            var provider = new SqlScalerProvider(providerWithDefaultFactory, CreateTriggerMetadata());

            Assert.IsType<SqlTriggerScaleMonitor>(provider.GetMonitor());
            Assert.IsType<SqlTriggerTargetScaler>(provider.GetTargetScaler());
            defaultFactory.Verify(value => value.CreateTokenCredential(It.IsAny<IConfiguration>()), Times.Never);
        }

        [Fact]
        public void Constructor_WithFactoryOverrideReturningNullCredential_Throws()
        {
            const string ConnectionString = "Server=server;Database=database;Authentication=Active Directory Managed Identity";
            TriggerMetadata triggerMetadata = CreateTriggerMetadata();
            triggerMetadata.Properties[nameof(AzureComponentFactory)] = new TestAzureComponentFactory(null);
            using ServiceProvider serviceProvider = CreateServiceProvider(ConnectionString);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new SqlScalerProvider(serviceProvider, triggerMetadata));

            Assert.Contains($"null {nameof(TokenCredential)}", exception.Message);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Constructor_WithInvalidFactoryOverride_Throws(bool nullValue)
        {
            const string ConnectionString = "Server=server;Database=database;Authentication=Active Directory Managed Identity";
            TriggerMetadata triggerMetadata = CreateTriggerMetadata();
            triggerMetadata.Properties[nameof(AzureComponentFactory)] = nullValue ? null : new object();
            using ServiceProvider serviceProvider = CreateServiceProvider(ConnectionString);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new SqlScalerProvider(serviceProvider, triggerMetadata));

            Assert.Contains($"must be an {nameof(AzureComponentFactory)}", exception.Message);
        }

        [Fact]
        public void Constructor_WithFactoryFailure_PropagatesException()
        {
            var exception = new InvalidOperationException("Credential configuration failed.");
            var componentFactory = new Mock<AzureComponentFactory>();
            componentFactory.Setup(value => value.CreateTokenCredential(It.IsAny<IConfiguration>())).Throws(exception);
            TriggerMetadata triggerMetadata = CreateTriggerMetadata();
            triggerMetadata.Properties[nameof(AzureComponentFactory)] = componentFactory.Object;
            using ServiceProvider serviceProvider = CreateServiceProvider("Server=server;Authentication=Active Directory MSI");

            InvalidOperationException result = Assert.Throws<InvalidOperationException>(() => new SqlScalerProvider(serviceProvider, triggerMetadata));

            Assert.Same(exception, result);
        }

        [Fact]
        public void Constructor_WithSqlAuthenticationAndCredentialOverride_Throws()
        {
            TriggerMetadata triggerMetadata = CreateTriggerMetadata();
            triggerMetadata.Properties[nameof(AzureComponentFactory)] = new TestAzureComponentFactory(new TestTokenCredential());
            using ServiceProvider serviceProvider = CreateServiceProvider("Server=server;Authentication=Sql Password");

            Assert.Throws<InvalidOperationException>(() => new SqlScalerProvider(serviceProvider, triggerMetadata));
        }

        private static TriggerMetadata CreateTriggerMetadata()
        {
            var metadata = JObject.Parse(
                @"{
                    ""type"": ""sqlTrigger"",
                    ""tableName"": ""[dbo].[Products]"",
                    ""connectionStringSetting"": ""SqlConnectionString"",
                    ""userFunctionId"": ""testFunctionId""
                }");
            return new TriggerMetadata(metadata);
        }

        private static ServiceProvider CreateServiceProvider(string connectionString, string settingName = "SqlConnectionString")
        {
            var configurationValues = new Dictionary<string, string>
            {
                [settingName] = connectionString,
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configurationValues)
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton(configuration);
            services.AddLogging();
            services.Configure<SqlOptions>(_ => { });
            return services.BuildServiceProvider();
        }

        private sealed class TestAzureComponentFactory : AzureComponentFactory
        {
            private readonly TokenCredential _credential;

            public TestAzureComponentFactory(TokenCredential credential)
            {
                this._credential = credential;
            }

            public int CreateTokenCredentialCallCount { get; private set; }

            public IConfiguration LastConfiguration { get; private set; }

            public override TokenCredential CreateTokenCredential(IConfiguration configuration)
            {
                this.CreateTokenCredentialCallCount++;
                this.LastConfiguration = configuration;
                return this._credential;
            }

            public override object CreateClientOptions(Type optionsType, object serviceVersion, IConfiguration configuration)
            {
                throw new NotSupportedException();
            }

            public override object CreateClient(Type clientType, IConfiguration configuration, TokenCredential credential, object clientOptions)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class TestTokenCredential : TokenCredential
        {
            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                return new AccessToken("token", DateTimeOffset.UtcNow.AddHours(1));
            }

            public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                return new ValueTask<AccessToken>(this.GetToken(requestContext, cancellationToken));
            }
        }
    }
}