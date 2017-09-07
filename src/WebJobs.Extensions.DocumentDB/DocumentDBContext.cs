// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal class DocumentDBContext
    {
        public DocumentDBAttribute ResolvedAttribute { get; set; }
        public IDocumentDBService Service { get; set; }
        public TraceWriter Trace { get; set; }
    }
}
