// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Documents.ChangeFeedProcessor;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Defines the [CosmosDBTrigger] attribute
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1019:DefineAccessorsForAttributeArguments")]
    [AttributeUsage(AttributeTargets.Parameter)]
    [Binding]
    public sealed class CosmosDBTriggerAttribute : Attribute
    {
        /// <summary>
        /// Triggers an event when changes occur on a monitored collection
        /// </summary>
        /// <param name="databaseName">Name of the database of the collection to monitor for changes</param>
        /// <param name="collectionName">Name of the collection to monitor for changes</param>
        public CosmosDBTriggerAttribute(string databaseName, string collectionName)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                throw new ArgumentException("Missing information for the collection to monitor", "collectionName");
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new ArgumentException("Missing information for the collection to monitor", "databaseName");
            }

            CollectionName = collectionName;
            DatabaseName = databaseName;
            LeaseCollectionName = CosmosDBTriggerConstants.DefaultLeaseCollectionName;
            LeaseDatabaseName = this.DatabaseName;
        }

        /// <summary>
        /// Connection string for the service containing the collection to monitor
        /// </summary>
        [AppSetting]
        public string ConnectionStringSetting { get; set; }

        /// <summary>
        /// Name of the collection to monitor for changes
        /// </summary>
        public string CollectionName { get; private set; }

        /// <summary>
        /// Name of the database containing the collection to monitor for changes
        /// </summary>
        public string DatabaseName { get; private set; }

        /// <summary>
        /// Connection string for the service containing the lease collection
        /// </summary>
        [AppSetting]
        public string LeaseConnectionStringSetting { get; set; }

        /// <summary>
        /// Name of the lease collection. Default value is "leases"
        /// </summary>
        public string LeaseCollectionName { get; set; }

        /// <summary>
        /// Name of the database containing the lease collection
        /// </summary>
        public string LeaseDatabaseName { get; set; }

        /// <summary>
        /// Gets or sets the lease options for this particular DocumentDB Trigger. Overrides any value defined in <see cref="DocumentDBConfiguration" /> 
        /// </summary>
        public ChangeFeedHostOptions LeaseOptions { get; set; }

        /// <summary>
        /// Optional.
        /// Only applies to lease collection.
        /// If true, the database and collection for leases will be automatically created if it does not exist.
        /// </summary>
        public bool CreateLeaseCollectionIfNotExists { get; set; } = false;

        /// <summary>
        /// Optional.
        /// When specified on an output binding and <see cref="CreateLeaseCollectionIfNotExists"/> is true, defines the throughput of the created
        /// collection.
        /// </summary>
        public int LeasesCollectionThroughput { get; set; }

        /// <summary>
        /// Optional, defaults to false.
        /// If false, any exception thrown by the function will be ignored and the checkpoint updated.
        /// If true, throwing an exception from the function will cause the Feed Processor not to checkpoint. Processing will be halted for the 
        /// current partition for a brief time and then any notifications since the last checkpoint will be re-delivered.
        /// If set to true, the function should implement its own any poison document handling to prevent an un-processable notification from 
        /// permanently halting processing of the partition and potentially running up execution costs; this flag is meant to enable guaranteed
        /// processing of every notification, even in case of a temporary "downstream dependency failure" preventing the function from completing
        /// successfully.
        /// </summary>
        public bool HaltOnFailure { get; set; } = false;
    }
}
