// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Bindings
{
    internal class CosmosDBClientBuilder : IConverter<CosmosDBAttribute, DocumentClient>
    {
        private CosmosDBExtensionConfigProvider _config;

        public CosmosDBClientBuilder(CosmosDBExtensionConfigProvider config)
        {
            _config = config;
        }

        public DocumentClient Convert(CosmosDBAttribute attribute)
        {
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            string resolvedConnectionString = _config.ResolveConnectionString(attribute.ConnectionStringSetting);
            ICosmosDBService service = _config.GetService(resolvedConnectionString);

            return service.GetClient();
        }
    }
}
