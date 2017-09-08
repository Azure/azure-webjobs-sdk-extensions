// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal class DocumentDBAsyncCollector<T> : IAsyncCollector<T>
    {
        private DocumentDBContext _docDBContext;

        public DocumentDBAsyncCollector(DocumentDBContext docDBContext)
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
                DocumentClientException de;
                if (_docDBContext.ResolvedAttribute.CreateIfNotExists &&
                    DocumentDBUtility.TryGetDocumentClientException(ex, out de) &&
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
                await CreateIfNotExistAsync(_docDBContext);
                await UpsertDocument(_docDBContext, item);
            }
        }

        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // no-op
            return Task.FromResult(0);
        }

        internal static async Task UpsertDocument(DocumentDBContext context, T item)
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

        internal static async Task CreateIfNotExistAsync(DocumentDBContext context)
        {
            await CreateDatabaseIfNotExistsAsync(context.Service, context.ResolvedAttribute.DatabaseName);

            await CreateDocumentCollectionIfNotExistsAsync(context.Service, context.ResolvedAttribute.DatabaseName, context.ResolvedAttribute.CollectionName, context.ResolvedAttribute.PartitionKey, context.ResolvedAttribute.CollectionThroughput);
        }

        internal static async Task<Database> CreateDatabaseIfNotExistsAsync(IDocumentDBService service, string databaseName)
        {
            Uri databaseUri = UriFactory.CreateDatabaseUri(databaseName);
            Database database = service.CreateDatabaseQuery().Where(db => db.Id == databaseName).AsEnumerable().FirstOrDefault();

            if (database == null)
            {
                database = await service.CreateDatabaseAsync(new Database { Id = databaseName });
            }

            return database;
        }

        internal static async Task<DocumentCollection> CreateDocumentCollectionIfNotExistsAsync(IDocumentDBService service, string databaseName, string collectionName, string partitionKey, int throughput)
        {
            Uri databaseUri = UriFactory.CreateDatabaseUri(databaseName);
            DocumentCollection collection = service.CreateDocumentCollectionQuery(databaseUri).Where(c => c.Id == collectionName).AsEnumerable().FirstOrDefault();

            if (collection == null)
            {
                DocumentCollection documentCollection = new DocumentCollection
                {
                    Id = collectionName
                };

                if (!string.IsNullOrEmpty(partitionKey))
                {
                    documentCollection.PartitionKey.Paths.Add(partitionKey);
                }

                // If there is any throughput specified, pass it on. DocumentClient will throw with a 
                // descriptive message if the value does not meet the collection requirements.
                RequestOptions collectionOptions = null;
                if (throughput != 0)
                {
                    collectionOptions = new RequestOptions
                    {
                        OfferThroughput = throughput
                    };
                }
                collection = await service.CreateDocumentCollectionAsync(databaseUri, documentCollection, collectionOptions);
            }

            return collection;
        }
    }
}
