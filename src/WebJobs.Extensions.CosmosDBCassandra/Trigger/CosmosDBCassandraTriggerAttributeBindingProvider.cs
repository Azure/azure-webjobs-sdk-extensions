// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Cassandra;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra
{
    internal class CosmosDBCassandraTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly IConfiguration _configuration;
        private readonly INameResolver _nameResolver;
        private readonly CosmosDBCassandraOptions _options;
        private readonly ILogger _logger;
        private readonly CosmosDBCassandraExtensionConfigProvider _configProvider;


        public CosmosDBCassandraTriggerAttributeBindingProvider(IConfiguration configuration, INameResolver nameResolver, CosmosDBCassandraOptions options,
            CosmosDBCassandraExtensionConfigProvider configProvider, ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _nameResolver = nameResolver;
            _options = options;
            _configProvider = configProvider;
            _logger = loggerFactory.CreateLogger(LogCategories.CreateTriggerCategory("CosmosDB"));
        }

        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            // Tries to parse the context parameters and see if it belongs to this [CosmosDBTrigger] binder
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            CosmosDBCassandraTriggerAttribute attribute = parameter.GetCustomAttribute<CosmosDBCassandraTriggerAttribute>(inherit: false);
            if (attribute == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            string contactPoint = ResolveConfigurationValue(attribute.ContactPoint, nameof(attribute.ContactPoint));
            string user = ResolveConfigurationValue(attribute.User, nameof(attribute.User));
            string password = ResolveConfigurationValue(attribute.Password, nameof(attribute.Password));

            ICosmosDBCassandraService cosmosDBCassandraService = _configProvider.GetService(contactPoint, user, password);

            return Task.FromResult((ITriggerBinding)new CosmosDBCassandraTriggerBinding(
                parameter,
                ResolveAttributeValue(attribute.KeyspaceName),
                ResolveAttributeValue(attribute.TableName),
                attribute.StartFromBeginning,
                attribute.FeedPollDelay,
                cosmosDBCassandraService,
                _logger));
        }

        internal string ResolveConfigurationValue(string unresolvedConnectionString, string propertyName)
        {
            // First, resolve the string.
            if (!string.IsNullOrEmpty(unresolvedConnectionString))
            {
                string resolvedString = _configuration.GetConnectionStringOrSetting(unresolvedConnectionString);

                if (string.IsNullOrEmpty(resolvedString))
                {
                    throw new InvalidOperationException($"Unable to resolve app setting for property '{nameof(CosmosDBCassandraTriggerAttribute)}.{propertyName}'. Make sure the app setting exists and has a valid value.");
                }

                return resolvedString;
            }

            throw new ArgumentNullException(propertyName);
        }

        private string ResolveAttributeValue(string attributeValue)
        {
            return _nameResolver.ResolveWholeString(attributeValue) ?? attributeValue;
        }
    }
}