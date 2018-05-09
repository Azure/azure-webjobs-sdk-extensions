// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class DefaultCosmosDBServiceFactory : ICosmosDBServiceFactory
    {
        public ICosmosDBService CreateService(string connectionString, ConnectionMode? connectionMode, Protocol? protocol)
        {
            return new CosmosDBService(connectionString, connectionMode, protocol);
        }
    }
}
