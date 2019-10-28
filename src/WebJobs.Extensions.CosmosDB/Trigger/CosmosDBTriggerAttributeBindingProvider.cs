// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerAttributeBindingProvider<T>
    {
        private const string CosmosDBTriggerUserAgentSuffix = "CosmosDBTriggerFunctions";
        private const string SharedThroughputRequirementException = "Shared throughput collection should have a partition key";
        private const string LeaseCollectionRequiredPartitionKey = "/id";
        private readonly IConfiguration _configuration;
        private readonly INameResolver _nameResolver;
        private readonly CosmosDBOptions _options;
        private readonly ILogger _logger;
        private readonly CosmosDBExtensionConfigProvider _configProvider;

        public CosmosDBTriggerAttributeBindingProvider(IConfiguration configuration, INameResolver nameResolver, CosmosDBOptions options,
            CosmosDBExtensionConfigProvider configProvider, ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _nameResolver = nameResolver;
            _options = options;
            _configProvider = configProvider;
            _logger = loggerFactory.CreateLogger(LogCategories.CreateTriggerCategory("CosmosDB"));
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
            string monitoredCollectionName = ResolveAttributeValue(attribute.CollectionName);
            string leasesDatabaseName = ResolveAttributeValue(attribute.LeaseDatabaseName);
            string leasesCollectionName = ResolveAttributeValue(attribute.LeaseCollectionName);
            string processorName = ResolveAttributeValue(attribute.LeaseCollectionPrefix) ?? string.Empty;

            try
            {
                string triggerConnectionString = ResolveAttributeConnectionString(attribute);
                if (string.IsNullOrEmpty(triggerConnectionString))
                {
                    throw new InvalidOperationException("The connection string for the monitored collection is in an invalid format, please use AccountEndpoint=XXXXXX;AccountKey=XXXXXX;.");
                }

                string leasesConnectionString = ResolveAttributeLeasesConnectionString(attribute);
                if (string.IsNullOrEmpty(leasesConnectionString))
                {
                    throw new InvalidOperationException("The connection string for the leases collection is in an invalid format, please use AccountEndpoint=XXXXXX;AccountKey=XXXXXX;.");
                }

                if (string.IsNullOrEmpty(monitoredDatabaseName)
                    || string.IsNullOrEmpty(monitoredCollectionName)
                    || string.IsNullOrEmpty(leasesDatabaseName)
                    || string.IsNullOrEmpty(leasesCollectionName))
                {
                    throw new InvalidOperationException("Cannot establish database and collection values. If you are using environment and configuration values, please ensure these are correctly set.");
                }

                if (triggerConnectionString.Equals(leasesConnectionString, StringComparison.InvariantCultureIgnoreCase)
                    && monitoredDatabaseName.Equals(leasesDatabaseName, StringComparison.InvariantCultureIgnoreCase)
                    && monitoredCollectionName.Equals(leasesCollectionName, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new InvalidOperationException("The monitored collection cannot be the same as the collection storing the leases.");
                }

                CosmosClient monitoredCosmosDBService = _configProvider.GetService(
                    connectionString: triggerConnectionString, 
                    preferredLocations: attribute.PreferredLocations, 
                    userAgent: CosmosDBTriggerUserAgentSuffix);
                CosmosClient leaseCosmosDBService = _configProvider.GetService(
                    connectionString: leasesConnectionString, 
                    preferredLocations: attribute.PreferredLocations, 
                    userAgent: CosmosDBTriggerUserAgentSuffix);

                if (attribute.CreateLeaseCollectionIfNotExists)
                {
                    await CreateLeaseCollectionIfNotExistsAsync(leaseCosmosDBService, leasesDatabaseName, leasesCollectionName, attribute.LeasesCollectionThroughput);
                }

                monitoredContainer = monitoredCosmosDBService.GetContainer(monitoredDatabaseName, monitoredCollectionName);
                leasesContainer = leaseCosmosDBService.GetContainer(leasesDatabaseName, leasesCollectionName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format("Cannot create Collection Information for {0} in database {1} with lease {2} in database {3} : {4}", attribute.CollectionName, attribute.DatabaseName, attribute.LeaseCollectionName, attribute.LeaseDatabaseName, ex.Message), ex);
            }

            return new CosmosDBTriggerBinding<T>(
                parameter,
                processorName,
                monitoredContainer,
                leasesContainer, 
                attribute,
                _logger);
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

        private static async Task CreateLeaseCollectionIfNotExistsAsync(CosmosClient cosmosClient, string databaseName, string collectionName, int? throughput)
        {
            await CosmosDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(cosmosClient, databaseName, collectionName, LeaseCollectionRequiredPartitionKey, throughput);
        }

        private string ResolveAttributeConnectionString(CosmosDBTriggerAttribute attribute)
        {
            string connectionString = ResolveConnectionString(attribute.ConnectionStringSetting, nameof(CosmosDBTriggerAttribute.ConnectionStringSetting));

            if (string.IsNullOrEmpty(connectionString))
            {
                ThrowMissingConnectionStringException();
            }

            return connectionString;
        }

        private string ResolveAttributeLeasesConnectionString(CosmosDBTriggerAttribute attribute)
        {
            // If the lease connection string is not set, use the trigger's
            string keyToResolve = attribute.LeaseConnectionStringSetting;
            if (string.IsNullOrEmpty(keyToResolve))
            {
                keyToResolve = attribute.ConnectionStringSetting;
            }

            string connectionString = ResolveConnectionString(keyToResolve, nameof(CosmosDBTriggerAttribute.LeaseConnectionStringSetting));

            if (string.IsNullOrEmpty(connectionString))
            {
                ThrowMissingConnectionStringException(true);
            }

            return connectionString;
        }

        private void ThrowMissingConnectionStringException(bool isLeaseConnectionString = false)
        {
            string attributeProperty = isLeaseConnectionString ?
                $"{nameof(CosmosDBTriggerAttribute)}.{nameof(CosmosDBTriggerAttribute.LeaseConnectionStringSetting)}" :
                $"{nameof(CosmosDBTriggerAttribute)}.{nameof(CosmosDBTriggerAttribute.ConnectionStringSetting)}";

            string optionsProperty = $"{nameof(CosmosDBOptions)}.{nameof(CosmosDBOptions.ConnectionString)}";

            string leaseString = isLeaseConnectionString ? "lease " : string.Empty;

            throw new InvalidOperationException(
                $"The CosmosDBTrigger {leaseString}connection string must be set either via a '{Constants.DefaultConnectionStringName}' configuration connection string, via the {attributeProperty} property or via {optionsProperty}.");
        }

        internal string ResolveConnectionString(string unresolvedConnectionString, string propertyName)
        {
            // First, resolve the string.
            if (!string.IsNullOrEmpty(unresolvedConnectionString))
            {
                string resolvedString = _configuration.GetConnectionStringOrSetting(unresolvedConnectionString);

                if (string.IsNullOrEmpty(resolvedString))
                {
                    throw new InvalidOperationException($"Unable to resolve app setting for property '{nameof(CosmosDBTriggerAttribute)}.{propertyName}'. Make sure the app setting exists and has a valid value.");
                }

                return resolvedString;
            }

            // If that didn't exist, fall back to options.
            return _options.ConnectionString;
        }

        private string ResolveAttributeValue(string attributeValue)
        {
            return _nameResolver.ResolveWholeString(attributeValue) ?? attributeValue;
        }
    }
}