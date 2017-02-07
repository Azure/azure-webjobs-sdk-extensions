// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB.Bindings
{
    internal class DocumentDBClientBuilder : IConverter<DocumentDBAttribute, DocumentClient>
    {
        private DocumentDBConfiguration _config;

        public DocumentDBClientBuilder(DocumentDBConfiguration config)
        {
            _config = config;
        }

        public DocumentClient Convert(DocumentDBAttribute attribute)
        {
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            string resolvedConnectionString = _config.ResolveConnectionString(attribute.ConnectionStringSetting);
            IDocumentDBService service = _config.GetService(resolvedConnectionString);

            return service.GetClient();
        }
    }
}
