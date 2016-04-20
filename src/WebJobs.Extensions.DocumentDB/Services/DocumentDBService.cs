// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB.Config;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal sealed class DocumentDBService : IDocumentDBService, IDisposable
    {
        private bool _isDisposed;
        private DocumentClient _client;

        public DocumentDBService(string connectionString)
        {
            DocumentDBConnectionString connection = new DocumentDBConnectionString(connectionString);
            _client = new DocumentClient(connection.ServiceEndpoint, connection.AuthKey);
        }

        public DocumentClient GetClient()
        {
            return _client;
        }

        public IOrderedQueryable<Database> CreateDatabaseQuery()
        {
            return _client.CreateDatabaseQuery();
        }

        public IOrderedQueryable<DocumentCollection> CreateDocumentCollectionQuery(Uri collectionUri)
        {
            return _client.CreateDocumentCollectionQuery(collectionUri);
        }

        public async Task<DocumentCollection> CreateDocumentCollectionAsync(Uri databaseUri, DocumentCollection documentCollection, RequestOptions options)
        {
            ResourceResponse<DocumentCollection> response = await _client.CreateDocumentCollectionAsync(databaseUri, documentCollection, options);
            return response.Resource;
        }

        public async Task<Database> CreateDatabaseAsync(Database database)
        {
            ResourceResponse<Database> response = await _client.CreateDatabaseAsync(database);
            return response.Resource;
        }

        public async Task<Document> UpsertDocumentAsync(Uri documentCollectionUri, object document)
        {
            ResourceResponse<Document> response = await _client.UpsertDocumentAsync(documentCollectionUri, document);
            return response.Resource;
        }

        public async Task<Document> ReplaceDocumentAsync(Uri documentUri, object document)
        {
            ResourceResponse<Document> response = await _client.ReplaceDocumentAsync(documentUri, document);
            return response.Resource;
        }

        public async Task<T> ReadDocumentAsync<T>(Uri documentUri, RequestOptions options)
        {
            ResourceResponse<Document> response = await _client.ReadDocumentAsync(documentUri, options);
            return (T)(dynamic)response.Resource;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                if (_client != null)
                {
                    _client.Dispose();
                    _client = null;
                }

                _isDisposed = true;
            }
        }
    }
}
