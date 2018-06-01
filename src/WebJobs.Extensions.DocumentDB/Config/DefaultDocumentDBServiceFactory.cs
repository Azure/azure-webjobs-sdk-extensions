// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal class DefaultDocumentDBServiceFactory : IDocumentDBServiceFactory
    {
        public IDocumentDBService CreateService(string connectionString, ConnectionMode? connectionMode, Protocol? protocol)
        {
            return new DocumentDBService(connectionString, connectionMode, protocol);
        }
    }
}
