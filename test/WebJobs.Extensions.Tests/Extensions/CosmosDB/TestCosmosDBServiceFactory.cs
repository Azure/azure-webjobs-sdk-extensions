// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.CosmosDB;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB
{
    internal class TestCosmosDBServiceFactory : ICosmosDBServiceFactory
    {
        private ICosmosDBService _service;

        public TestCosmosDBServiceFactory(ICosmosDBService service)
        {
            _service = service;
        }

        public ICosmosDBService CreateService(string connectionString)
        {
            return _service;
        }
    }
}