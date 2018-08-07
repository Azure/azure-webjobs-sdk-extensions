// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to bind to an Azure CosmosDB collection.
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
        /// <param name="databaseName">The CosmosDB database name.</param>
        /// <param name="collectionName">The CosmosDB collection name.</param>
        public CosmosDBAttribute(string databaseName, string collectionName)
        {
            DatabaseName = databaseName;
            CollectionName = collectionName;
        }

        /// <summary>
        /// The name of the database to which the parameter applies.        
        /// May include binding parameters.
        /// </summary>
        [AutoResolve]
        public string DatabaseName { get; private set; }

        /// <summary>
        /// The name of the collection to which the parameter applies. 
        /// May include binding parameters.
        /// </summary>
        [AutoResolve]
        public string CollectionName { get; private set; }

        /// <summary>
        /// Optional.
        /// Only applies to output bindings.
        /// If true, the database and collection will be automatically created if they do not exist.
        /// </summary>
        public bool CreateIfNotExists { get; set; }

        /// <summary>
        /// Optional. A string value indicating the app setting to use as the CosmosDB connection string, if different
        /// than the one specified in the <see cref="CosmosDBOptions"/>.
        /// </summary>
        [AppSetting]
        public string ConnectionStringSetting { get; set; }

        /// <summary>
        /// Optional. The Id of the document to retrieve from the collection.
        /// May include binding parameters.
        /// </summary>
        [AutoResolve]
        public string Id { get; set; }

        /// <summary>
        /// Optional.
        /// When specified on an output binding and <see cref="CreateIfNotExists"/> is true, defines the partition key 
        /// path for the created collection.
        /// When specified on an input binding, specifies the partition key value for the lookup.
        /// May include binding parameters.
        /// </summary>
        [AutoResolve]
        public string PartitionKey { get; set; }

        /// <summary>
        /// Optional.
        /// When specified on an output binding and <see cref="CreateIfNotExists"/> is true, defines the throughput of the created
        /// collection.
        /// </summary>
        public int CollectionThroughput { get; set; }

        /// <summary>
        /// Optional.
        /// When specified on an input binding using an <see cref="IEnumerable{T}"/>, defines the query to run against the collection. 
        /// May include binding parameters.
        /// </summary>
        [AutoResolve(ResolutionPolicyType = typeof(CosmosDBSqlResolutionPolicy))]
        public string SqlQuery { get; set; }

        internal SqlParameterCollection SqlQueryParameters { get; set; }
    }
}