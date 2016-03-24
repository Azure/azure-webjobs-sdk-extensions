// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    /// <summary>
    /// An abstraction layer for communicating with a DocumentDB account.
    /// </summary>
    internal interface IDocumentDBService
    {
        /// <summary>
        /// Creates the specified <see cref="Database"/>.
        /// </summary>
        /// <param name="database">The <see cref="Database"/> to create.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<Database> CreateDatabaseAsync(Database database);

        /// <summary>
        /// Creates the specified <see cref="DocumentCollection"/>.
        /// </summary>
        /// <param name="databaseUri">The self-link of the database to create the collection in.</param>
        /// <param name="documentCollection">The <see cref="DocumentCollection"/> to create.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<DocumentCollection> CreateDocumentCollectionAsync(Uri databaseUri, DocumentCollection documentCollection);

        /// <summary>
        /// Inserts or replaces a document.
        /// </summary>
        /// <param name="documentCollectionUri">The self-link of the collection to create the document in.</param>
        /// <param name="document">The document object.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<Document> UpsertDocumentAsync(Uri documentCollectionUri, object document);

        /// <summary>
        /// Reads a document.
        /// </summary>
        /// <param name="documentUri">The self-link of the document.</param>
        /// <returns>The task object representing the service response for the asynchronous operation.</returns>
        Task<T> ReadDocumentAsync<T>(Uri documentUri);

        /// <summary>
        /// Replaces a document.
        /// </summary>
        /// <param name="documentUri">The self-link of the collection to create the document in.</param>
        /// <param name="document">The <see cref="Document"/> to replace.</param>
        /// <returns></returns>
        Task<Document> ReplaceDocumentAsync(Uri documentUri, object document);

        /// <summary>
        /// Returns the underlying <see cref="DocumentClient"/>.
        /// </summary>
        /// <returns></returns>
        DocumentClient GetClient();
    }
}