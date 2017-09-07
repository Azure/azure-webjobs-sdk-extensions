// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal class DocumentDBEnumerableBuilder<T> : IAsyncConverter<DocumentDBAttribute, IEnumerable<T>> where T : class
    {
        private readonly DocumentDBConfiguration _config;

        public DocumentDBEnumerableBuilder(DocumentDBConfiguration config)
        {
            _config = config;
        }

        public async Task<IEnumerable<T>> ConvertAsync(DocumentDBAttribute attribute, CancellationToken cancellationToken)
        {
            DocumentDBContext context = _config.CreateContext(attribute);

            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(context.ResolvedAttribute.DatabaseName, context.ResolvedAttribute.CollectionName);

            List<T> finalResults = new List<T>();

            string continuation = null;

            SqlQuerySpec sqlSpec = new SqlQuerySpec
            {
                QueryText = context.ResolvedAttribute.SqlQuery,
                Parameters = context.ResolvedAttribute.SqlQueryParameters ?? new SqlParameterCollection()
            };

            do
            {
                DocumentQueryResponse<T> response = await context.Service.ExecuteNextAsync<T>(collectionUri, sqlSpec, continuation);

                finalResults.AddRange(response.Results);
                continuation = response.ResponseContinuation;
            }
            while (!string.IsNullOrEmpty(continuation));

            return finalResults;
        }
    }
}
