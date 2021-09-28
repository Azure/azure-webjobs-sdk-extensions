// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerAttributeBindingProvider<T>
    {
        private const string CosmosDBTriggerUserAgentSuffix = "CosmosDBTriggerFunctions";
        private const string SharedThroughputRequirementException = "Shared throughput collection should have a partition key";
        private const string LeaseCollectionRequiredPartitionKey = "/id";
        private const string LeaseCollectionRequiredPartitionKeyFromGremlin = "/partitionKey";
        private readonly INameResolver _nameResolver;
        private readonly CosmosDBOptions _options;
        private readonly ILogger _logger;
        private readonly CosmosDBExtensionConfigProvider _configProvider;
        private readonly IFunctionDataCache _functionDataCache;

        public CosmosDBTriggerAttributeBindingProvider(INameResolver nameResolver, CosmosDBOptions options,
            CosmosDBExtensionConfigProvider configProvider, ILoggerFactory loggerFactory, IFunctionDataCache functionDataCache)
        {
            _nameResolver = nameResolver;
            _options = options;
            _configProvider = configProvider;
            _logger = loggerFactory.CreateLogger(LogCategories.CreateTriggerCategory("CosmosDB"));
            _functionDataCache = functionDataCache;
        }

        public async Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            // Tries to parse the context parameters and see if it belongs to this [CosmosDBTrigger] binder
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            CosmosDBTriggerAttribute attribute = parameter.GetCustomAttribute<CosmosDBTriggerAttribute>(inherit: false);
            if (attribute == null)
            {
                return null;
            }

            Container monitoredContainer;
            Container leasesContainer;
            string monitoredDatabaseName = ResolveAttributeValue(attribute.DatabaseName);
            string monitoredCollectionName = ResolveAttributeValue(attribute.ContainerName);
            string leasesDatabaseName = ResolveAttributeValue(attribute.LeaseDatabaseName);
            string leasesCollectionName = ResolveAttributeValue(attribute.LeaseContainerName);
            string processorName = ResolveAttributeValue(attribute.LeaseContainerPrefix) ?? string.Empty;
            string preferredLocations = ResolveAttributeValue(attribute.PreferredLocations);

            try
            {
                string triggerConnection = ResolveAttributeConnection(attribute);
                if (string.IsNullOrEmpty(triggerConnection))
                {
                    throw new InvalidOperationException($"The attribute {nameof(attribute.Connection)} for the monitored container is in an invalid format, please use AccountEndpoint=XXXXXX;AccountKey=XXXXXX; or a node representing token authentication information.");
                }

                string leasesConnection = ResolveAttributeLeasesConnection(attribute);
                if (string.IsNullOrEmpty(leasesConnection))
                {
                    throw new InvalidOperationException($"The attribute {nameof(attribute.LeaseConnection)} for the leases container is in an invalid format, please use AccountEndpoint=XXXXXX;AccountKey=XXXXXX;. or a node representing token authentication information.");
                }

                if (string.IsNullOrEmpty(monitoredDatabaseName)
                    || string.IsNullOrEmpty(monitoredCollectionName)
                    || string.IsNullOrEmpty(leasesDatabaseName)
                    || string.IsNullOrEmpty(leasesCollectionName))
                {
                    throw new InvalidOperationException("Cannot establish database and container values. If you are using environment and configuration values, please ensure these are correctly set.");
                }

                if (triggerConnection.Equals(leasesConnection, StringComparison.InvariantCultureIgnoreCase)
                    && monitoredDatabaseName.Equals(leasesDatabaseName, StringComparison.InvariantCultureIgnoreCase)
                    && monitoredCollectionName.Equals(leasesCollectionName, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new InvalidOperationException("The monitored container cannot be the same as the container storing the leases.");
                }

                CosmosClient monitoredCosmosDBService = _configProvider.GetService(
                    connection: triggerConnection, 
                    preferredLocations: preferredLocations, 
                    userAgent: CosmosDBTriggerUserAgentSuffix);
                CosmosClient leaseCosmosDBService = _configProvider.GetService(
                    connection: leasesConnection, 
                    preferredLocations: preferredLocations, 
                    userAgent: CosmosDBTriggerUserAgentSuffix);

                if (attribute.CreateLeaseContainerIfNotExists)
                {
                    await CreateLeaseCollectionIfNotExistsAsync(leaseCosmosDBService, leasesDatabaseName, leasesCollectionName, attribute.LeasesContainerThroughput);
                }

                monitoredContainer = monitoredCosmosDBService.GetContainer(monitoredDatabaseName, monitoredCollectionName);
                leasesContainer = leaseCosmosDBService.GetContainer(leasesDatabaseName, leasesCollectionName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format("Cannot create container information for {0} in database {1} with lease {2} in database {3} : {4}", attribute.ContainerName, attribute.DatabaseName, attribute.LeaseContainerName, attribute.LeaseDatabaseName, ex.Message), ex);
            }

            return new CosmosDBTriggerBinding<T>(
                parameter,
                processorName,
                monitoredContainer,
                leasesContainer, 
                attribute,
                _logger,
                _functionDataCache);
        }

        internal static TimeSpan ResolveTimeSpanFromMilliseconds(string nameOfProperty, TimeSpan baseTimeSpan, int? attributeValue)
        {
            if (!attributeValue.HasValue || attributeValue.Value == 0)
            {
                return baseTimeSpan;
            }

            if (attributeValue.Value < 0)
            {
                throw new InvalidOperationException($"'{nameOfProperty}' must be greater than 0.");
            }

            return TimeSpan.FromMilliseconds(attributeValue.Value);
        }

        private static async Task CreateLeaseCollectionIfNotExistsAsync(CosmosClient cosmosClient, string databaseName, string collectionName, int throughput)
        {
            try
            {
                await CosmosDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(cosmosClient, databaseName, collectionName, LeaseCollectionRequiredPartitionKey, throughput);
            }
            catch (CosmosException cosmosException) 
                when (cosmosException.StatusCode == System.Net.HttpStatusCode.BadRequest
                    && cosmosException.Message.Contains("invalid for Gremlin API"))
            {
                await CosmosDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(cosmosClient, databaseName, collectionName, LeaseCollectionRequiredPartitionKeyFromGremlin, throughput);
            }
        }

        private string ResolveAttributeConnection(CosmosDBTriggerAttribute attribute)
        {
            string connectionString = attribute.Connection ?? Constants.DefaultConnectionStringName;

            if (string.IsNullOrEmpty(connectionString))
            {
                ThrowMissingConnectionStringException();
            }

            return connectionString;
        }

        private string ResolveAttributeLeasesConnection(CosmosDBTriggerAttribute attribute)
        {
            // If the lease connection string is not set, use the trigger's
            string keyToResolve = attribute.LeaseConnection;
            if (string.IsNullOrEmpty(keyToResolve))
            {
                keyToResolve = attribute.Connection;
            }

            string connectionString = keyToResolve ?? Constants.DefaultConnectionStringName;

            if (string.IsNullOrEmpty(connectionString))
            {
                ThrowMissingConnectionStringException(true);
            }

            return connectionString;
        }

        private void ThrowMissingConnectionStringException(bool isLeaseConnectionString = false)
        {
            string attributeProperty = isLeaseConnectionString ?
                $"{nameof(CosmosDBTriggerAttribute)}.{nameof(CosmosDBTriggerAttribute.LeaseConnection)}" :
                $"{nameof(CosmosDBTriggerAttribute)}.{nameof(CosmosDBTriggerAttribute.Connection)}";

            string leaseString = isLeaseConnectionString ? "lease " : string.Empty;

            throw new InvalidOperationException(
                $"The CosmosDBTrigger {leaseString}connection must be set either via a '{Constants.DefaultConnectionStringName}' configuration or via the {attributeProperty} property.");
        }

        private string ResolveAttributeValue(string attributeValue)
        {
            return _nameResolver.ResolveWholeString(attributeValue) ?? attributeValue;
        }
    }
}