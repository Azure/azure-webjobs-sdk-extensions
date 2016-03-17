// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB.Config;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal class DocumentDBService : IDocumentDBService
    {
        private DocumentClient _client;

        public DocumentDBService(string connectionString)
        {
            DocumentDBConnectionString connection = new DocumentDBConnectionString(connectionString);
            _client = new DocumentClient(connection.ServiceEndpoint, connection.AuthKey);
        }

        public async Task<DocumentCollection> CreateDocumentCollectionAsync(Uri databaseUri, DocumentCollection documentCollection)
        {
            ResourceResponse<DocumentCollection> response = await _client.CreateDocumentCollectionAsync(databaseUri, documentCollection);
            return response.Resource;
        }

        public async Task<Database> CreateDatabaseAsync(Database database)
        {
            ResourceResponse<Database> response = await _client.CreateDatabaseAsync(database);
            return response.Resource;
        }

        public async Task<Document> CreateDocumentAsync(Uri documentCollectionUri, object document)
        {
            ResourceResponse<Document> response = await _client.CreateDocumentAsync(documentCollectionUri, document);
            return response.Resource;
        }
    }
}
