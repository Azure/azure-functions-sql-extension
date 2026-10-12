// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Data.SqlClient;

namespace Microsoft.Azure.WebJobs.Extensions.Sql
{
    internal sealed class SqlConnectionFactory
    {
        private const string DefaultScopeSuffix = "/.default";
        private readonly string _connectionString;
        private readonly Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> _accessTokenCallback;

        public SqlConnectionFactory(string connectionString, TokenCredential credential = null)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            this._connectionString = credential == null ? connectionString : NormalizeConnectionString(connectionString);
            if (credential != null)
            {
                this._accessTokenCallback = async (authenticationParameters, cancellationToken) =>
                {
                    string resource = authenticationParameters.Resource;
                    string scope = resource.EndsWith(DefaultScopeSuffix, StringComparison.OrdinalIgnoreCase)
                        ? resource
                        : $"{resource.TrimEnd('/')}{DefaultScopeSuffix}";
                    AccessToken accessToken = await credential.GetTokenAsync(
                        new TokenRequestContext(new[] { scope }),
                        cancellationToken).ConfigureAwait(false);
                    return new SqlAuthenticationToken(accessToken.Token, accessToken.ExpiresOn);
                };
            }
        }

        public SqlConnection CreateConnection()
        {
            var connection = new SqlConnection(this._connectionString);
            if (this._accessTokenCallback != null)
            {
                connection.AccessTokenCallback = this._accessTokenCallback;
            }

            return connection;
        }

        private static string NormalizeConnectionString(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            if (connectionStringBuilder.IntegratedSecurity || !string.IsNullOrEmpty(connectionStringBuilder.Password))
            {
                throw new InvalidOperationException("Integrated security and password authentication are not compatible with the injected TokenCredential.");
            }

            SqlAuthenticationMethod authentication = connectionStringBuilder.Authentication;
            if (authentication != SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
                && authentication != SqlAuthenticationMethod.ActiveDirectoryMSI)
            {
                throw new InvalidOperationException($"Authentication method '{authentication}' is not compatible with the injected TokenCredential.");
            }

            connectionStringBuilder.Remove("Authentication");
            return connectionStringBuilder.ConnectionString;
        }
    }
}