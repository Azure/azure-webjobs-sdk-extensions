// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBContext
    {
        public CosmosDBAttribute ResolvedAttribute { get; set; }
        public ICosmosDBService Service { get; set; }
        public TraceWriter Trace { get; set; }
    }
}
