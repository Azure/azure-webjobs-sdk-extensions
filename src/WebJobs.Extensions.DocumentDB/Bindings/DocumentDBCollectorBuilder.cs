// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB.Bindings
{
    internal class DocumentDBCollectorBuilder<T> : IConverter<DocumentDBAttribute, IAsyncCollector<T>>
    {
        private DocumentDBConfiguration _config;

        public DocumentDBCollectorBuilder(DocumentDBConfiguration config)
        {
            _config = config;
        }

        public IAsyncCollector<T> Convert(DocumentDBAttribute attribute)
        {
            DocumentDBContext context = _config.CreateContext(attribute);
            return new DocumentDBAsyncCollector<T>(context);
        }
    }
}
