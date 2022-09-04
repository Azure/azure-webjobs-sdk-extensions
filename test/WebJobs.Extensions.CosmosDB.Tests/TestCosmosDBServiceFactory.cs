// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Cosmos;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    internal class TestCosmosDBServiceFactory : ICosmosDBServiceFactory
    {
        private CosmosClient _service;

        public TestCosmosDBServiceFactory(CosmosClient service)
        {
            _service = service;
        }

        public CosmosClient CreateService(string connectionString, CosmosClientOptions options)
        {
            return _service;
        }
    }
}