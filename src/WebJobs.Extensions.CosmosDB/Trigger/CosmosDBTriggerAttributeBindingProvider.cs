// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.ChangeFeedProcessor;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB.Config;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerAttributeBindingProvider : ITriggerBindingProvider
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

            ConnectionMode? desiredConnectionMode = _options.ConnectionMode;
            Protocol? desiredConnectionProtocol = _options.Protocol;

            DocumentCollectionInfo documentCollectionLocation;
            DocumentCollectionInfo leaseCollectionLocation;
            ChangeFeedProcessorOptions processorOptions = BuildProcessorOptions(attribute);
            
            processorOptions.StartFromBeginning = attribute.StartFromBeginning;

            if (attribute.MaxItemsPerInvocation > 0)
            {
                processorOptions.MaxItemCount = attribute.MaxItemsPerInvocation;
            }

            if (DateTime.TryParse(attribute.StartFrom, out var startFromDateTime))
            {
                processorOptions.StartTime = startFromDateTime;
            }

            ICosmosDBService monitoredCosmosDBService;
            ICosmosDBService leaseCosmosDBService;

            try
            {
                string triggerConnectionString = ResolveAttributeConnectionString(attribute);
                CosmosDBConnectionString triggerConnection = new CosmosDBConnectionString(triggerConnectionString);
                if (triggerConnection.ServiceEndpoint == null)
                {
                    throw new InvalidOperationException("The connection string for the monitored collection is in an invalid format, please use AccountEndpoint=XXXXXX;AccountKey=XXXXXX;.");
                }

                string leasesConnectionString = ResolveAttributeLeasesConnectionString(attribute);
                CosmosDBConnectionString leasesConnection = new CosmosDBConnectionString(leasesConnectionString);
                if (leasesConnection.ServiceEndpoint == null)
                {
                    throw new InvalidOperationException("The connection string for the leases collection is in an invalid format, please use AccountEndpoint=XXXXXX;AccountKey=XXXXXX;.");
                }

                documentCollectionLocation = new DocumentCollectionInfo
                {
                    Uri = triggerConnection.ServiceEndpoint,
                    MasterKey = triggerConnection.AuthKey,
                    DatabaseName = ResolveAttributeValue(attribute.DatabaseName),
                    CollectionName = ResolveAttributeValue(attribute.CollectionName)
                };

                documentCollectionLocation.ConnectionPolicy.UserAgentSuffix = CosmosDBTriggerUserAgentSuffix;

                if (desiredConnectionMode.HasValue)
                {
                    documentCollectionLocation.ConnectionPolicy.ConnectionMode = desiredConnectionMode.Value;
                }

                if (desiredConnectionProtocol.HasValue)
                {
                    documentCollectionLocation.ConnectionPolicy.ConnectionProtocol = desiredConnectionProtocol.Value;
                }

                leaseCollectionLocation = new DocumentCollectionInfo
                {
                    Uri = leasesConnection.ServiceEndpoint,
                    MasterKey = leasesConnection.AuthKey,
                    DatabaseName = ResolveAttributeValue(attribute.LeaseDatabaseName),
                    CollectionName = ResolveAttributeValue(attribute.LeaseCollectionName)
                };

                leaseCollectionLocation.ConnectionPolicy.UserAgentSuffix = CosmosDBTriggerUserAgentSuffix;

                if (desiredConnectionMode.HasValue)
                {
                    leaseCollectionLocation.ConnectionPolicy.ConnectionMode = desiredConnectionMode.Value;
                }

                if (desiredConnectionProtocol.HasValue)
                {
                    leaseCollectionLocation.ConnectionPolicy.ConnectionProtocol = desiredConnectionProtocol.Value;
                }

                string resolvedPreferredLocations = ResolveAttributeValue(attribute.PreferredLocations);
                foreach (var location in CosmosDBUtility.ParsePreferredLocations(resolvedPreferredLocations))
                {
                    documentCollectionLocation.ConnectionPolicy.PreferredLocations.Add(location);
                    leaseCollectionLocation.ConnectionPolicy.PreferredLocations.Add(location);
                }

                leaseCollectionLocation.ConnectionPolicy.UseMultipleWriteLocations = attribute.UseMultipleWriteLocations;

                if (string.IsNullOrEmpty(documentCollectionLocation.DatabaseName)
                    || string.IsNullOrEmpty(documentCollectionLocation.CollectionName)
                    || string.IsNullOrEmpty(leaseCollectionLocation.DatabaseName)
                    || string.IsNullOrEmpty(leaseCollectionLocation.CollectionName))
                {
                    throw new InvalidOperationException("Cannot establish database and collection values. If you are using environment and configuration values, please ensure these are correctly set.");
                }

                if (documentCollectionLocation.Uri.Equals(leaseCollectionLocation.Uri)
                    && documentCollectionLocation.DatabaseName.Equals(leaseCollectionLocation.DatabaseName)
                    && documentCollectionLocation.CollectionName.Equals(leaseCollectionLocation.CollectionName))
                {
                    throw new InvalidOperationException("The monitored collection cannot be the same as the collection storing the leases.");
                }

                monitoredCosmosDBService = _configProvider.GetService(
                    connectionString: triggerConnectionString, 
                    preferredLocations: resolvedPreferredLocations, 
                    useMultipleWriteLocations: false, 
                    useDefaultJsonSerialization: attribute.UseDefaultJsonSerialization, 
                    userAgent: CosmosDBTriggerUserAgentSuffix);
                leaseCosmosDBService = _configProvider.GetService(
                    connectionString: leasesConnectionString,
                    preferredLocations: resolvedPreferredLocations,
                    useMultipleWriteLocations: attribute.UseMultipleWriteLocations,
                    useDefaultJsonSerialization: false, // Lease collection operations should not be affected by serialization configuration
                    userAgent: CosmosDBTriggerUserAgentSuffix);

                if (attribute.CreateLeaseCollectionIfNotExists)
                {
                    await CreateLeaseCollectionIfNotExistsAsync(leaseCosmosDBService, leaseCollectionLocation.DatabaseName, leaseCollectionLocation.CollectionName, attribute.LeasesCollectionThroughput);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format("Cannot create Collection Information for {0} in database {1} with lease {2} in database {3} : {4}", attribute.CollectionName, attribute.DatabaseName, attribute.LeaseCollectionName, attribute.LeaseDatabaseName, ex.Message), ex);
            }

            return new CosmosDBTriggerBinding(
                parameter, 
                documentCollectionLocation, 
                leaseCollectionLocation, 
                processorOptions, 
                monitoredCosmosDBService,
                leaseCosmosDBService,
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

        private static async Task CreateLeaseCollectionIfNotExistsAsync(ICosmosDBService leaseCosmosDBService, string databaseName, string collectionName, int throughput)
        {
            try
            {
                await CosmosDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(leaseCosmosDBService, databaseName, collectionName, null, throughput);
            }
            catch (DocumentClientException ex) when (ex.Message.Contains(SharedThroughputRequirementException))
            {
                await CosmosDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(leaseCosmosDBService, databaseName, collectionName, LeaseCollectionRequiredPartitionKey, throughput);
            }
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

        private ChangeFeedProcessorOptions BuildProcessorOptions(CosmosDBTriggerAttribute attribute)
        {
            ChangeFeedHostOptions leasesOptions = _options.LeaseOptions;

            ChangeFeedProcessorOptions processorOptions = new ChangeFeedProcessorOptions
            {
                LeasePrefix = ResolveAttributeValue(attribute.LeaseCollectionPrefix) ?? leasesOptions.LeasePrefix,
                FeedPollDelay = ResolveTimeSpanFromMilliseconds(nameof(CosmosDBTriggerAttribute.FeedPollDelay), leasesOptions.FeedPollDelay, attribute.FeedPollDelay),
                LeaseAcquireInterval = ResolveTimeSpanFromMilliseconds(nameof(CosmosDBTriggerAttribute.LeaseAcquireInterval), leasesOptions.LeaseAcquireInterval, attribute.LeaseAcquireInterval),
                LeaseExpirationInterval = ResolveTimeSpanFromMilliseconds(nameof(CosmosDBTriggerAttribute.LeaseExpirationInterval), leasesOptions.LeaseExpirationInterval, attribute.LeaseExpirationInterval),
                LeaseRenewInterval = ResolveTimeSpanFromMilliseconds(nameof(CosmosDBTriggerAttribute.LeaseRenewInterval), leasesOptions.LeaseRenewInterval, attribute.LeaseRenewInterval),
                CheckpointFrequency = leasesOptions.CheckpointFrequency ?? new CheckpointFrequency()
            };

            if (attribute.CheckpointInterval > 0)
            {
                processorOptions.CheckpointFrequency.TimeInterval = TimeSpan.FromMilliseconds(attribute.CheckpointInterval);
            }

            if (attribute.CheckpointDocumentCount > 0)
            {
                processorOptions.CheckpointFrequency.ProcessedDocumentCount = attribute.CheckpointDocumentCount;
            }

            return processorOptions;
        }

        private string ResolveAttributeValue(string attributeValue)
        {
            return _nameResolver.ResolveWholeString(attributeValue) ?? attributeValue;
        }
    }
}