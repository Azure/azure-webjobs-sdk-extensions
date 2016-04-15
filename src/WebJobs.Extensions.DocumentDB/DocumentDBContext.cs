// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal class DocumentDBContext
    {
        private const int DefaultMaxThrottleRetries = 10;

        public DocumentDBContext()
        {
            MaxThrottleRetries = DefaultMaxThrottleRetries;
        }

        public IDocumentDBService Service { get; set; }
        public string ResolvedDatabaseName { get; set; }
        public string ResolvedCollectionName { get; set; }
        public string ResolvedId { get; set; }
        public int MaxThrottleRetries { get; set; }
        public TraceWriter Trace { get; set; }
        public string ResolvedPartitionKey { get; set; }
        public bool CreateIfNotExists { get; set; }
        public int CollectionThroughput { get; set; }
    }
}
