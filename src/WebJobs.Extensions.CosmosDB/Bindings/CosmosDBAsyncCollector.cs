// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBAsyncCollector<T> : IAsyncCollector<T>
    {
        private CosmosDBContext _docDBContext;

        public CosmosDBAsyncCollector(CosmosDBContext docDBContext)
        {
            _docDBContext = docDBContext;
        }

        public async Task AddAsync(T item, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                await UpsertDocument(_docDBContext, item);
            }
            catch (Exception ex)
            {
                if (CosmosDBUtility.TryGetCosmosException(ex, out CosmosException de) &&
                    de.StatusCode == HttpStatusCode.NotFound)
                {
                    if (_docDBContext.ResolvedAttribute.CreateIfNotExists)
                    {
                        await CosmosDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(_docDBContext);

                        await UpsertDocument(_docDBContext, item);
                    }
                    else
                    {
                        // Throw a custom error so that it's easier to decipher.
                        string message = $"The container '{_docDBContext.ResolvedAttribute.ContainerName}' (in database '{_docDBContext.ResolvedAttribute.DatabaseName}') does not exist. To automatically create the container, set '{nameof(CosmosDBAttribute.CreateIfNotExists)}' to 'true'.";
                        throw new InvalidOperationException(message, ex);
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // no-op
            return Task.FromResult(0);
        }

        internal static Task UpsertDocument(CosmosDBContext context, T item)
        {
            // Support user sending a string
            if (item is string)
            {
                JObject asJObject = JObject.Parse(item.ToString());
                return context.Service.GetContainer(context.ResolvedAttribute.DatabaseName, context.ResolvedAttribute.ContainerName).UpsertItemAsync(asJObject);
            }

            return context.Service.GetContainer(context.ResolvedAttribute.DatabaseName, context.ResolvedAttribute.ContainerName).UpsertItemAsync(item);
        }
    }
}
