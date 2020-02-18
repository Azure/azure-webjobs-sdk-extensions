// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra.Tests
{
    internal class TestCosmosDBServiceFactory : ICosmosDBCassandraServiceFactory
    {
        private ICosmosDBCassandraService _service;

        public TestCosmosDBServiceFactory(ICosmosDBCassandraService service)
        {
            _service = service;
        }

        public ICosmosDBCassandraService CreateService(string connectionString, string user, string password)
        {
            return _service;
        }
    }
}