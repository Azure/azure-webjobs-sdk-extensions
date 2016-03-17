// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;

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
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(_docDBContext.ResolvedDatabaseName, _docDBContext.ResolvedCollectionName);

            await DocumentDBUtility.ExecuteWithRetriesAsync(async () =>
            {
                return await _docDBContext.Service.CreateDocumentAsync(collectionUri, item);
            });
        }

        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            // no-op
            return Task.FromResult(0);
        }
    }
}
