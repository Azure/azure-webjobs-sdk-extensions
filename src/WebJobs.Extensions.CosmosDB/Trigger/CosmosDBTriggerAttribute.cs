// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Defines the [CosmosDBTrigger] attribute.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1019:DefineAccessorsForAttributeArguments")]
    [AttributeUsage(AttributeTargets.Parameter)]
    [Binding]
    public sealed class CosmosDBTriggerAttribute : Attribute
    {
        /// <summary>
        /// Triggers an event when changes occur on a monitored container.
        /// </summary>
        /// <param name="databaseName">Name of the database of the container to monitor for changes.</param>
        /// <param name="containerName">Name of the container to monitor for changes.</param>
        public CosmosDBTriggerAttribute(string databaseName, string containerName)
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException("Missing information for the container to monitor", "containerName");
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new ArgumentException("Missing information for the container to monitor", "databaseName");
            }

            ContainerName = containerName;
            DatabaseName = databaseName;
            LeaseContainerName = CosmosDBTriggerConstants.DefaultLeaseCollectionName;
            LeaseDatabaseName = this.DatabaseName;
        }

        /// <summary>
        /// Gets or sets the connection string for the service containing the container to monitor.
        /// </summary>
        public string Connection { get; set; }

        /// <summary>
        /// Gets the name of the container to monitor for changes.
        /// </summary>
        public string ContainerName { get; private set; }

        /// <summary>
        /// Gets the name of the database containing the container to monitor for changes.
        /// </summary>
        public string DatabaseName { get; private set; }

        /// <summary>
        /// Gets or sets the connection string for the service containing the lease container.
        /// </summary>
        [ConnectionString]
        public string LeaseConnection { get; set; }

        /// <summary>
        /// Gets or sets the name of the lease container. Default value is "leases".
        /// </summary>
        public string LeaseContainerName { get; set; }

        /// <summary>
        /// Gets or sets the name of the database containing the lease container.
        /// </summary>
        public string LeaseDatabaseName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the database and container for leases will be automatically created if it does not exist.
        /// </summary>
        public bool CreateLeaseContainerIfNotExists { get; set; } = false;

        /// <summary>
        /// Gets or sets the throughput to be used when creating the container if <see cref="CreateLeaseContainerIfNotExists"/> is true.
        /// container.
        /// </summary>
        public int? LeasesContainerThroughput { get; set; }

        /// <summary>
        /// Gets or sets a prefix to be used within a Leases container for this Trigger. Useful when sharing the same Lease container among multiple Triggers.
        /// </summary>
        public string LeaseContainerPrefix { get; set; }
        
        /// <summary>
        /// Gets or sets the delay in milliseconds in between polling a partition for new changes on the feed, after all current changes are drained.  Default is 5000 (5 seconds).
        /// </summary>
        public int FeedPollDelay { get; set; }
        
        /// <summary>
        /// Gets or sets the renew interval in milliseconds for all leases for partitions currently held by the Trigger. Default is 17000 (17 seconds).
        /// </summary>
        public int LeaseRenewInterval { get; set; }
        
        /// <summary>
        /// Gets or sets the interval in milliseconds to kick off a task to compute if partitions are distributed evenly among known host instances. Default is 13000 (13 seconds).
        /// </summary>
        public int LeaseAcquireInterval { get; set; }

        /// <summary>
        /// Gets or sets the interval in milliseconds for which the lease is taken on a lease representing a partition. If the lease is not renewed within this interval, it will cause it to expire and ownership of the partition will move to another Trigger instance. Default is 60000 (60 seconds).
        /// </summary>
        public int LeaseExpirationInterval { get; set; }

        /// <summary>
        /// Gets or sets  the maximum amount of items received in an invocation.
        /// </summary>
        public int MaxItemsPerInvocation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether change feed in the Azure Cosmos DB service should start from beginning (true) or from current (false). By default it's start from current (false).
        /// </summary>
        /// <remarks>This is only used to set the initial trigger state. Once the trigger has a lease state, changing this value has no effect.</remarks>
        public bool StartFromBeginning { get; set; } = false;

        /// <summary>
        /// Gets or sets the a date and time to initialize the change feed read operation from.
        /// <remark>The recommended format is ISO 8601 with the UTC designator. For example: "2021-02-16T14:19:29Z"</remark>
        /// </summary>
        /// <remarks>This is only used to set the initial trigger state. Once the trigger has a lease state, changing this value has no effect.</remarks>
        public string StartFromTime { get; set; }

        /// <summary>
        /// Gets or sets the preferred locations (regions) for geo-replicated database accounts in the Azure Cosmos DB service.
        /// Values should be comma-separated.
        /// </summary>
        /// <example>
        /// PreferredLocations = "East US,South Central US,North Europe".
        /// </example>
        public string PreferredLocations { get; set; }
    }
}
