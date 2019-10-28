// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerMetrics : ScaleMetrics
    {
        public int PartitionCount { get; set; }

        public long RemainingWork { get; set; }
    }
}