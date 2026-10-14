// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Data.SqlClient;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Sql.Tests.Unit
{
    public class SqlConnectionFactoryTests
    {
        [Theory]
        [InlineData("Authentication=Active Directory Managed Identity;User ID=client-id")]
        [InlineData("Authentication=Active Directory MSI")]
        [InlineData("Authentication=Active Directory Default")]
        [InlineData("Authentication=Sql Password;User ID=sql-user;Password=test-password")]
        [InlineData("User ID=sql-user;Password=test-password")]
        [InlineData("Integrated Security=True")]
        public void CreateConnection_WithoutCredential_PreservesConnectionStringAuthentication(string authentication)
        {
            string connectionString = $"Server=server;Database=database;{authentication};Encrypt=True";
            var factory = new SqlConnectionFactory(connectionString);

            using SqlConnection connection = factory.CreateConnection();

            Assert.Equal(connectionString, connection.ConnectionString);
            Assert.Null(connection.AccessTokenCallback);
        }

        [Theory]
        [InlineData("Active Directory Managed Identity", null)]
        [InlineData("Active Directory MSI", null)]
        [InlineData("Active Directory Managed Identity", "client-id")]
        [InlineData("Active Directory MSI", "client-id")]
        public void CreateConnection_WithManagedIdentityAuthentication_RemovesAuthenticationAndPreservesOptions(string authentication, string clientId)
        {
            string connectionString = $"Server=tcp:server.database.windows.net,1433;Database=database;Authentication={authentication};Encrypt=True;TrustServerCertificate=False";
            if (clientId != null)
            {
                connectionString += $";UID={clientId}";
            }

            var factory = new SqlConnectionFactory(connectionString, new TestTokenCredential());

            using SqlConnection connection = factory.CreateConnection();

            var builder = new SqlConnectionStringBuilder(connection.ConnectionString);
            Assert.Equal(SqlAuthenticationMethod.NotSpecified, builder.Authentication);
            Assert.Equal(clientId ?? string.Empty, builder.UserID);
            Assert.Equal("tcp:server.database.windows.net,1433", builder.DataSource);
            Assert.Equal("database", builder.InitialCatalog);
            Assert.True(builder.Encrypt);
            Assert.False(builder.TrustServerCertificate);
            Assert.NotNull(connection.AccessTokenCallback);
        }

        [Theory]
        [InlineData("https://database.windows.net", "https://database.windows.net/.default")]
        [InlineData("https://database.windows.net/", "https://database.windows.net//.default")]
        [InlineData("https://database.windows.net/.default", "https://database.windows.net/.default")]
        [InlineData("https://database.windows.net//.default", "https://database.windows.net//.default")]
        [InlineData("https://database.usgovcloudapi.net/", "https://database.usgovcloudapi.net//.default")]
        public async Task AccessTokenCallback_UsesResourceScopeAndReturnsCredentialToken(string resource, string expectedScope)
        {
            DateTimeOffset expiresOn = DateTimeOffset.UtcNow.AddMinutes(30);
            var credential = new TestTokenCredential(new AccessToken("token-value", expiresOn));
            var factory = new SqlConnectionFactory("Server=server;Database=database;Authentication=Active Directory Managed Identity", credential);
            using SqlConnection connection = factory.CreateConnection();
            var authenticationParameters = new TestAuthenticationParameters(resource);

            SqlAuthenticationToken token = await connection.AccessTokenCallback(authenticationParameters, CancellationToken.None);

            Assert.Equal(expectedScope, Assert.Single(credential.LastRequestContext.Scopes));
            Assert.Equal(1, credential.GetTokenAsyncCallCount);
            Assert.Equal("token-value", token.AccessToken);
            Assert.Equal(expiresOn, token.ExpiresOn);
        }

        [Fact]
        public void CreateConnection_ReusesAccessTokenCallback()
        {
            var factory = new SqlConnectionFactory("Server=server;Database=database;Authentication=Active Directory MSI", new TestTokenCredential());

            using SqlConnection firstConnection = factory.CreateConnection();
            using SqlConnection secondConnection = factory.CreateConnection();

            Assert.Same(firstConnection.AccessTokenCallback, secondConnection.AccessTokenCallback);
        }

        [Fact]
        public void CreateConnection_DifferentCredentials_DoNotShareAccessTokenCallback()
        {
            const string ConnectionString = "Server=server;Database=database;Authentication=Active Directory Managed Identity";
            var firstFactory = new SqlConnectionFactory(ConnectionString, new TestTokenCredential());
            var secondFactory = new SqlConnectionFactory(ConnectionString, new TestTokenCredential());

            using SqlConnection firstConnection = firstFactory.CreateConnection();
            using SqlConnection secondConnection = secondFactory.CreateConnection();

            Assert.NotSame(firstConnection.AccessTokenCallback, secondConnection.AccessTokenCallback);
        }

        [Fact]
        public async Task AccessTokenCallback_PropagatesCredentialFailure()
        {
            var exception = new InvalidOperationException("Token acquisition failed.");
            var credential = new Mock<TokenCredential>();
            credential.Setup(value => value.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<AccessToken>(Task.FromException<AccessToken>(exception)));
            var factory = new SqlConnectionFactory("Server=server;Authentication=Active Directory Managed Identity", credential.Object);
            using SqlConnection connection = factory.CreateConnection();

            InvalidOperationException result = await Assert.ThrowsAsync<InvalidOperationException>(
                () => connection.AccessTokenCallback(new TestAuthenticationParameters(), CancellationToken.None));

            Assert.Same(exception, result);
        }

        [Fact]
        public async Task AccessTokenCallback_PropagatesCancellation()
        {
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();
            CancellationToken cancellationToken = cancellationSource.Token;
            var credential = new Mock<TokenCredential>(MockBehavior.Strict);
            credential.Setup(value => value.GetTokenAsync(It.IsAny<TokenRequestContext>(), cancellationToken))
                .Returns(new ValueTask<AccessToken>(Task.FromCanceled<AccessToken>(cancellationToken)));
            var factory = new SqlConnectionFactory("Server=server;Authentication=Active Directory Managed Identity", credential.Object);
            using SqlConnection connection = factory.CreateConnection();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => connection.AccessTokenCallback(new TestAuthenticationParameters(), cancellationToken));

            credential.Verify(value => value.GetTokenAsync(It.IsAny<TokenRequestContext>(), cancellationToken), Times.Once);
        }

        [Fact]
        public async Task AccessTokenCallback_DoesNotCacheExpiredTokens()
        {
            var credential = new Mock<TokenCredential>();
            DateTimeOffset expiresOn = DateTimeOffset.UtcNow.AddHours(1);
            credential.SetupSequence(value => value.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<AccessToken>(new AccessToken("old-token", DateTimeOffset.UtcNow.AddMinutes(-1))))
                .Returns(new ValueTask<AccessToken>(new AccessToken("new-token", expiresOn)));
            var factory = new SqlConnectionFactory("Server=server;Authentication=Active Directory Managed Identity", credential.Object);
            using SqlConnection connection = factory.CreateConnection();
            var parameters = new TestAuthenticationParameters();

            await connection.AccessTokenCallback(parameters, CancellationToken.None);
            SqlAuthenticationToken token = await connection.AccessTokenCallback(parameters, CancellationToken.None);

            Assert.Equal("new-token", token.AccessToken);
            Assert.Equal(expiresOn, token.ExpiresOn);
        }

        [Theory]
        [InlineData("Active Directory Default")]
        [InlineData("Active Directory Service Principal")]
        [InlineData("Sql Password")]
        [InlineData("Active Directory Workload Identity")]
        public void Constructor_WithIncompatibleAuthentication_Throws(string authentication)
        {
            string connectionString = $"Server=server;Database=database;Authentication={authentication}";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new SqlConnectionFactory(connectionString, new TestTokenCredential()));

            Assert.Contains("not compatible", exception.Message);
        }

        [Fact]
        public void Constructor_WithoutAuthentication_Throws()
        {
            const string ConnectionString = "Server=server;Database=database";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new SqlConnectionFactory(ConnectionString, new TestTokenCredential()));

            Assert.Contains("not compatible", exception.Message);
        }

        [Fact]
        public void Constructor_WithIntegratedSecurity_Throws()
        {
            const string ConnectionString = "Server=server;Database=database;Integrated Security=True";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new SqlConnectionFactory(ConnectionString, new TestTokenCredential()));

            Assert.Contains("Integrated security", exception.Message);
        }

        [Fact]
        public void Constructor_WithPassword_Throws()
        {
            const string ConnectionString = "Server=server;Database=database;User ID=sql-user;Password=password";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new SqlConnectionFactory(ConnectionString, new TestTokenCredential()));

            Assert.Contains("password authentication", exception.Message);
        }

        private sealed class TestAuthenticationParameters : SqlAuthenticationParameters
        {
            public TestAuthenticationParameters(string resource = "https://database.windows.net/")
                : base(SqlAuthenticationMethod.NotSpecified, "server", "database", resource, null, null, null, Guid.NewGuid(), 30)
            {
            }
        }

        private sealed class TestTokenCredential : TokenCredential
        {
            private readonly AccessToken _accessToken;

            public TestTokenCredential()
                : this(new AccessToken("token", DateTimeOffset.UtcNow.AddHours(1)))
            {
            }

            public TestTokenCredential(AccessToken accessToken)
            {
                this._accessToken = accessToken;
            }

            public TokenRequestContext LastRequestContext { get; private set; }

            public int GetTokenAsyncCallCount { get; private set; }

            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                this.LastRequestContext = requestContext;
                return this._accessToken;
            }

            public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                this.GetTokenAsyncCallCount++;
                this.LastRequestContext = requestContext;
                return new ValueTask<AccessToken>(this._accessToken);
            }
        }
    }
}