// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Cassandra;
using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra
{
    internal sealed class CosmosDBCassandraService : ICosmosDBCassandraService, IDisposable
    {
        private bool _isDisposed;
        private Cluster _cluster;

        public CosmosDBCassandraService(string contactPoint, string user, string password)
        {
            var options = new SSLOptions(SslProtocols.Tls12, true, ValidateServerCertificate);
            options.SetHostNameResolver((ipAddress) => contactPoint);
            _cluster = Cluster.Builder().WithCredentials(user, password).WithPort(10350).AddContactPoint(contactPoint).WithSSL(options).Build();
        }

        public Cluster GetCluster()
        {
            return _cluster;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                if (_cluster != null)
                {
                    _cluster.Dispose();
                    _cluster = null;
                }

                _isDisposed = true;
            }
        }

        private static bool ValidateServerCertificate(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            // Do not allow this client to communicate with unauthenticated servers.
            throw new InvalidOperationException($"Certificate error: {sslPolicyErrors}");
            
        }
    }
}
