// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
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
            bool create = false;
            try
            {
                await UpsertDocument(_docDBContext, item);
            }
            catch (Exception ex)
            {
                if (_docDBContext.ResolvedAttribute.CreateIfNotExists &&
                    CosmosDBUtility.TryGetDocumentClientException(ex, out DocumentClientException de) &&
                    de.StatusCode == HttpStatusCode.NotFound)
                {
                    create = true;
                }
                else
                {
                    throw;
                }
            }

            if (create)
            {
                await CosmosDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(_docDBContext);

                await UpsertDocument(_docDBContext, item);
            }
        }

        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // no-op
            return Task.FromResult(0);
        }

        internal static async Task UpsertDocument(CosmosDBContext context, T item)
        {
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(context.ResolvedAttribute.DatabaseName, context.ResolvedAttribute.CollectionName);

            // DocumentClient does not accept strings directly.
            object convertedItem = item;
            if (item is string)
            {
                convertedItem = JObject.Parse(item.ToString());
            }

            await context.Service.UpsertDocumentAsync(collectionUri, convertedItem);
        }
    }
}
