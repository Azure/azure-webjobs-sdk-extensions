// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Cosmos;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    /// <summary>
    /// Factory used to provide a custom <see cref="CosmosSerializer"/> to be used with client instances.
    /// </summary>
    public interface ICosmosDBSerializerFactory
    {
        /// <summary>
        /// Provides a custom implementation of <see cref="CosmosSerializer"/>.
        /// </summary>
        /// <returns></returns>
        CosmosSerializer CreateSerializer();
    }
}
