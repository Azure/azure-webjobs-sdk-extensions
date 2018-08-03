// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    internal class TestCosmosDBServiceFactory : ICosmosDBServiceFactory
    {
        private ICosmosDBService _service;

        public TestCosmosDBServiceFactory(ICosmosDBService service)
        {
            _service = service;
        }

        public ICosmosDBService CreateService(string connectionString, ConnectionMode? connectionMode, Protocol? protocol)
        {
            return _service;
        }
    }
}