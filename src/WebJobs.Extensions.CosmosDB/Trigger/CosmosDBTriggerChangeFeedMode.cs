// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#if PREVIEW
using Microsoft.Azure.Cosmos;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Represents the change feed mode for the Cosmos DB Trigger.
    /// This mirrors the SDK ChangeFeedMode options while remaining attribute-friendly.
    /// </summary>
    public enum CosmosDBTriggerChangeFeedMode
    {
        /// <summary>
        /// Only the latest version of each item is included. Deletes are not surfaced.
        /// </summary>
        LatestVersion = 0,

        /// <summary>
        /// All intermediate versions and delete tombstones are included (preview feature in Azure Cosmos DB).
        /// </summary>
        AllVersionsAndDeletes = 1
    }
}
#endif
