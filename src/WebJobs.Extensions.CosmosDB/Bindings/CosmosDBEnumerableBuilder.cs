// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBEnumerableBuilder<T> : IAsyncConverter<CosmosDBAttribute, IEnumerable<T>>
        where T : class
    {
        private readonly CosmosDBExtensionConfigProvider _configProvider;

        public CosmosDBEnumerableBuilder(CosmosDBExtensionConfigProvider configProvider)
        {
            _configProvider = configProvider;
        }

        public async Task<IEnumerable<T>> ConvertAsync(CosmosDBAttribute attribute, CancellationToken cancellationToken)
        {
            CosmosDBContext context = _configProvider.CreateContext(attribute);

            List<T> finalResults = new List<T>();

            Container container = context.Service.GetContainer(context.ResolvedAttribute.DatabaseName, context.ResolvedAttribute.ContainerName);

            QueryDefinition queryDefinition = null;
            if (!string.IsNullOrEmpty(attribute.SqlQuery))
            {
                queryDefinition = new QueryDefinition(attribute.SqlQuery);
                if (attribute.SqlQueryParameters != null)
                {
                    foreach (var parameter in attribute.SqlQueryParameters)
                    {
                        queryDefinition.WithParameter(parameter.Item1, parameter.Item2);
                    }
                }
            }

            // gochaudh:
            // At this point, we have the query and the container name.
            // We can check if the cache has a value associated with the result of that query, we can just pass an interface back
            // of ICacheAwareReadObject with the appropriate information.
            // From there on, azure-functions-host will know how to handle that.
            // Without any caching (in the default case), the return result of the query is JSON object which azure-functions-host::ScriptInvocationContextExtensions.cs::ToRpcInvocationRequest
            // knows how to handle. With the caching case, it will be similar to how blob is handled - the type would be JSON.
            // In SharedMemoryManager.cs::IsSupported we will say that the particular JSON type (Newtonsoft.Json.Linq.JArray) is supported and in PutObjectAsync we will convert it serialized into bytes appropriately.
            // Similarly in GetObjectAsync, if the worker sends JSON (in case of producing a CosmosDB output) we will add that to the types it supports (both in worker shared_memory_manager and SharedMemoryManager.cs)
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions();
            if (!string.IsNullOrEmpty(attribute.PartitionKey))
            {
                queryRequestOptions.PartitionKey = new PartitionKey(attribute.PartitionKey);
            }

            using (FeedIterator<T> iterator = container.GetItemQueryIterator<T>(queryDefinition: queryDefinition, requestOptions: queryRequestOptions))
            {
                while (iterator.HasMoreResults)
                {
                    FeedResponse<T> response = await iterator.ReadNextAsync(cancellationToken);
                    finalResults.AddRange(response.Resource);
                }

                return finalResults;
            }
        }
    }
}
