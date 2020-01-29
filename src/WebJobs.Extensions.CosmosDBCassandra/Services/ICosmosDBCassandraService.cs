// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Cassandra;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra
{
    /// <summary>
    /// An abstraction layer for communicating with a CosmosDB Cassandra account.
    /// </summary>
    internal interface ICosmosDBCassandraService
    {
        /// <summary>
        /// Returns the underlying <see cref="Cluster"/>.
        /// </summary>
        /// <returns></returns>
        Cluster GetCluster();
    }
}