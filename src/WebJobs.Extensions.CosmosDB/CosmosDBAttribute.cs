// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to bind to an Azure Cosmos DB account.
    /// </summary>
    /// <remarks>
    /// The method parameter type can be one of the following:
    /// <list type="bullet">
    /// <item><description><see cref="ICollector{T}"/></description></item>
    /// <item><description><see cref="IAsyncCollector{T}"/></description></item>
    /// <item><description>out T</description></item>
    /// <item><description>out T[]</description></item>
    /// </list>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    [Binding]
    public sealed class CosmosDBAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public CosmosDBAttribute()
        {
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="databaseName">The Azure Cosmos database name.</param>
        /// <param name="containerName">The Azure Cosmos container name.</param>
        public CosmosDBAttribute(string databaseName, string containerName)
        {
            DatabaseName = databaseName;
            ContainerName = containerName;
        }

        /// <summary>
        /// Gets the name of the database to which the parameter applies.        
        /// May include binding parameters.
        /// </summary>
        [AutoResolve]
        public string DatabaseName { get; private set; }

        /// <summary>
        /// Gets the name of the container to which the parameter applies. 
        /// May include binding parameters.
        /// </summary>
        [AutoResolve]
        public string ContainerName { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the database and container will be automatically created if they do not exist.
        /// </summary>
        /// <remarks>
        /// Only applies to output bindings.
        /// </remarks>
        public bool CreateIfNotExists { get; set; }

        /// <summary>
        /// Gets or sets the connection string for the service containing the container to monitor.
        /// </summary>
        [ConnectionString]
        public string Connection { get; set; }

        /// <summary>
        /// Gets or sets the Id of the document to retrieve from the container.
        /// May include binding parameters.
        /// </summary>
        [AutoResolve]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the partition key path to be used if <see cref="CreateIfNotExists"/> is true on an output binding.
        /// When specified on an input binding, specifies the partition key value for the lookup.
        /// May include binding parameters.
        /// </summary>
        [AutoResolve]
        public string PartitionKey { get; set; }

        /// <summary>
        /// Gets or sets the throughput to be used when creating the container if <see cref="CreateIfNotExists"/> is true.
        /// </summary>
        public int? ContainerThroughput { get; set; }

        /// <summary>
        /// Gets or sets a sql query expression for an input binding to execute on the container and produce results.
        /// May include binding parameters.
        /// </summary>
        [AutoResolve(ResolutionPolicyType = typeof(CosmosDBSqlResolutionPolicy))]
        public string SqlQuery { get; set; }

        /// <summary>
        /// Gets or sets the preferred locations (regions) for geo-replicated database accounts in the Azure Cosmos DB service.
        /// Values should be comma-separated.
        /// </summary>
        /// <example>
        /// PreferredLocations = "East US,South Central US,North Europe".
        /// </example>
        [AutoResolve]
        public string PreferredLocations { get; set; }

        internal IEnumerable<(string, object)> SqlQueryParameters { get; set; }
    }
}