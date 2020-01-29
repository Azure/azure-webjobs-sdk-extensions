// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra
{
    internal class DefaultCosmosDBCassandraServiceFactory : ICosmosDBCassandraServiceFactory
    {
        public ICosmosDBCassandraService CreateService(string contactPoint, string user, string password)
        {
            return new CosmosDBCassandraService(contactPoint, user, password);
        }
    }
}
