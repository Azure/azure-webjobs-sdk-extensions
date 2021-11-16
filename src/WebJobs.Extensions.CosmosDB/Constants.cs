// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    public static class Constants
    {
        public const string DefaultConnectionStringName = "CosmosDB";
    }

    internal static class Events
    {
        public static readonly EventId OnError = new EventId(1, "OnTriggerError");
        public static readonly EventId OnAcquire = new EventId(2, "OnTriggerAcquire");
        public static readonly EventId OnRelease = new EventId(3, "OnTriggerRelease");
        public static readonly EventId OnDelivery = new EventId(4, "OnTriggerDelivery");
        public static readonly EventId OnListenerStopError = new EventId(5, "OnTriggerListenerStopError");
        public static readonly EventId OnScaling = new EventId(6, "OnScaling");
    }
}
