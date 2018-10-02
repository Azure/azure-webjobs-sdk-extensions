// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Bindings
{
    internal class CosmosDBClientBuilder : IConverter<CosmosDBAttribute, DocumentClient>
    {
        private readonly CosmosDBExtensionConfigProvider _configProvider;

        public CosmosDBClientBuilder(CosmosDBExtensionConfigProvider configProvider)
        {
            _configProvider = configProvider;
        }

        public DocumentClient Convert(CosmosDBAttribute attribute)
        {
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            string resolvedConnectionString = _configProvider.ResolveConnectionString(attribute.ConnectionStringSetting);
            ICosmosDBService service = _configProvider.GetService(resolvedConnectionString, attribute.PreferredLocations, attribute.UseMultipleWriteLocations);

            return service.GetClient();
        }
    }
}
