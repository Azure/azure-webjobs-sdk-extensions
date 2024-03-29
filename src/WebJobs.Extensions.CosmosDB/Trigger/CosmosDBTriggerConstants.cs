﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal static class CosmosDBTriggerConstants
    {
        public const string DefaultLeaseCollectionName = "leases";

        public const string TriggerName = "CosmosDBTrigger";

        public const string TriggerDescription = "New changes on container {0} at {1}";

        public const string InvokeString = "{0} changes detected.";
    }
}