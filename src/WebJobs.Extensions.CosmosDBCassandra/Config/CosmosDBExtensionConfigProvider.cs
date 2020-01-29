// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Cassandra;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra
{
    /// <summary>
    /// Defines the configuration options for the CosmosDB Cassandra binding.
    /// </summary>
    [Extension("CosmosDBCassandra")]
    internal class CosmosDBExtensionConfigProvider : IExtensionConfigProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ICosmosDBCassandraServiceFactory _cosmosDBServiceFactory;
        private readonly INameResolver _nameResolver;
        private readonly CosmosDBCassandraOptions _options;
        private readonly ILoggerFactory _loggerFactory;

        public CosmosDBExtensionConfigProvider(IOptions<CosmosDBCassandraOptions> options, ICosmosDBCassandraServiceFactory cosmosDBServiceFactory,  IConfiguration configuration, INameResolver nameResolver, ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _cosmosDBServiceFactory = cosmosDBServiceFactory;
            _nameResolver = nameResolver;
            _options = options.Value;
            _loggerFactory = loggerFactory;
        }

        internal ConcurrentDictionary<string, ICosmosDBCassandraService> ClientCache { get; } = new ConcurrentDictionary<string, ICosmosDBCassandraService>();

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            // Trigger
            var rule2 = context.AddBindingRule<CosmosDBCassandraTriggerAttribute>();
            rule2.BindToTrigger<IReadOnlyList<Row>>(new CosmosDBCassandraTriggerAttributeBindingProvider(_configuration, _nameResolver, _options, this, _loggerFactory));
            //rule2.AddConverter<string, RowSet>(str => JsonConvert.DeserializeObject<IReadOnlyList<Row>>(str));
            //rule2.AddConverter<RowSet, JArray>(docList => JArray.FromObject(docList));
            //rule2.AddConverter<RowSet, string>(docList => JArray.FromObject(docList).ToString());
        }

        internal ICosmosDBCassandraService GetService(string contactPoint, string user, string password)
        {
            string cacheKey = BuildCacheKey(contactPoint, user, password);
            return ClientCache.GetOrAdd(cacheKey, (c) => _cosmosDBServiceFactory.CreateService(contactPoint, user, password));
        }

        internal static string BuildCacheKey(string contactPoint, string user, string password) => $"{contactPoint}|{user}|{password}";
    }
}