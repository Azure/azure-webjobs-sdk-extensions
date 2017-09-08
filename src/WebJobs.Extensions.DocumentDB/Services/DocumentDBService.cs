// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
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

        public async Task<DocumentCollection> CreateDocumentCollectionIfNotExistsAsync(Uri databaseUri, DocumentCollection documentCollection, RequestOptions options)
        {
            ResourceResponse<DocumentCollection> response = await _client.CreateDocumentCollectionIfNotExistsAsync(databaseUri, documentCollection, options);
            return response.Resource;
        }

        public async Task<Database> CreateDatabaseIfNotExistsAsync(Database database)
        {
            ResourceResponse<Database> response = await _client.CreateDatabaseIfNotExistsAsync(database);
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

        public async Task<Document> ReadDocumentAsync(Uri documentUri, RequestOptions options)
        {
            ResourceResponse<Document> response = await _client.ReadDocumentAsync(documentUri, options);
            return response.Resource;
        }

        public async Task<DocumentQueryResponse<T>> ExecuteNextAsync<T>(Uri documentCollectionUri, SqlQuerySpec sqlSpec, string continuation)
        {
            FeedOptions feedOptions = new FeedOptions { RequestContinuation = continuation, EnableCrossPartitionQuery = true };

            IDocumentQuery<T> query = null;
            if (sqlSpec?.QueryText == null)
            {
                query = _client.CreateDocumentQuery<T>(documentCollectionUri, feedOptions).AsDocumentQuery();
            }
            else
            {
                query = _client.CreateDocumentQuery<T>(documentCollectionUri, sqlSpec, feedOptions).AsDocumentQuery();
            }

            FeedResponse<T> response = await query.ExecuteNextAsync<T>();

            return new DocumentQueryResponse<T>
            {
                Results = response,
                ResponseContinuation = response.ResponseContinuation
            };
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
