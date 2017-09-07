// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class DefaultCosmosDBServiceFactory : ICosmosDBServiceFactory
    {
        public ICosmosDBService CreateService(string connectionString)
        {
            return new CosmosDBService(connectionString);
        }
    }
}
