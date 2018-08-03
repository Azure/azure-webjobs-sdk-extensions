// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Documents.ChangeFeedProcessor;
using Microsoft.Azure.Documents.Client;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    public class CosmosDBOptions
    {
        /// <summary>
        /// Gets or sets the CosmosDB connection string.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the ConnectionMode used in the DocumentClient instances.
        /// </summary>
        public ConnectionMode? ConnectionMode { get; set; }

        /// <summary>
        /// Gets or sets the Protocol used in the DocumentClient instances.
        /// </summary>
        public Protocol? Protocol { get; set; }

        /// <summary>
        /// Gets or sets the lease options for the DocumentDB Trigger. 
        /// </summary>
        public ChangeFeedHostOptions LeaseOptions { get; set; } = new ChangeFeedHostOptions();
    }
}