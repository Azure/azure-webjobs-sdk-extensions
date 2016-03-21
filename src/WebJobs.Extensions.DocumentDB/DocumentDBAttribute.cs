// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to bind to an Azure DocumentDB collection.
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
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class DocumentDBAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public DocumentDBAttribute()
        {
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="databaseName">The DocumentDB database name.</param>
        /// <param name="collectionName">The DocumentDB collection name.</param>
        public DocumentDBAttribute(string databaseName, string collectionName)
        {
            DatabaseName = databaseName;
            CollectionName = collectionName;
        }

        /// <summary>
        /// The name of the database to which the parameter applies.        
        /// May include binding parameters.
        /// </summary>
        public string DatabaseName { get; private set; }

        /// <summary>
        /// The name of the collection to which the parameter applies. 
        /// May include binding parameters.
        /// </summary>
        public string CollectionName { get; private set; }

        /// <summary>
        /// If true, the database and collection will be automatically created if they do not exist.
        /// </summary>
        public bool CreateIfNotExists { get; set; }

        /// <summary>
        /// Optional. A string value indicating the app setting to use as the DocumentDB connection string, if different
        /// than the one specified in the <see cref="DocumentDBConfiguration"/>.
        /// </summary>
        public string ConnectionString { get; set; }
    }
}