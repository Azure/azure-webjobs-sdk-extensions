// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Bindings
{
    internal class CosmosDBCollectorBuilder<T> : IConverter<CosmosDBAttribute, IAsyncCollector<T>>
    {
        private readonly CosmosDBExtensionConfigProvider _configProvider;

        public CosmosDBCollectorBuilder(CosmosDBExtensionConfigProvider configProvider)
        {
            _configProvider = configProvider;
        }

        public IAsyncCollector<T> Convert(CosmosDBAttribute attribute)
        {
            CosmosDBContext context = _configProvider.CreateContext(attribute);
            return new CosmosDBAsyncCollector<T>(context);
        }
    }
}
