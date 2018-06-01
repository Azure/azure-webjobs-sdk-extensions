// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    internal class TestDocumentDBServiceFactory : IDocumentDBServiceFactory
    {
        private IDocumentDBService _service;

        public TestDocumentDBServiceFactory(IDocumentDBService service)
        {
            _service = service;
        }

        public IDocumentDBService CreateService(string connectionString, ConnectionMode? connectionMode, Protocol? protocol)
        {
            return _service;
        }
    }
}