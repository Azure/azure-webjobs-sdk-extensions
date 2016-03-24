// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal class DocumentDBContext
    {
        public IDocumentDBService Service { get; set; }
        public string ResolvedDatabaseName { get; set; }
        public string ResolvedCollectionName { get; set; }
        public string ResolvedId { get; set; }
    }
}
