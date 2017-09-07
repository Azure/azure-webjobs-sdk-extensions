// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Bindings
{
    internal class CosmosDBCollectorBuilder<T> : IConverter<CosmosDBAttribute, IAsyncCollector<T>>
    {
        private CosmosDBConfiguration _config;

        public CosmosDBCollectorBuilder(CosmosDBConfiguration config)
        {
            _config = config;
        }

        public IAsyncCollector<T> Convert(CosmosDBAttribute attribute)
        {
            CosmosDBContext context = _config.CreateContext(attribute);
            return new CosmosDBAsyncCollector<T>(context);
        }
    }
}
