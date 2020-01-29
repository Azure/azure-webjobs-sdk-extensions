// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra
{
    internal static class CosmosDBCassandraTriggerConstants
    {
        public const string DefaultLeaseCollectionName = "leases";

        public const string TriggerName = "CosmosDBCassandraTrigger";

        public const string TriggerDescription = "New changes on keyspace {0} table {1} at {2}";

        public const string InvokeString = "{0} changes detected.";
    }
}