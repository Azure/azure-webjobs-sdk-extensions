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
