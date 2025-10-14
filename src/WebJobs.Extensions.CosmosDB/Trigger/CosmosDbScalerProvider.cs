// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using static Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger.CosmosDbScalerProvider;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger
{
    internal class CosmosDbScalerProvider : IScaleMonitorProvider, ITargetScalerProvider
    {
        private readonly CosmosDBScaleMonitor _scaleMonitor;
        private readonly CosmosDBTargetScaler _targetScaler;

        public CosmosDbScalerProvider(IServiceProvider serviceProvider, TriggerMetadata triggerMetadata) 
        {
            AzureComponentFactory azureComponentFactory = null;
            if (triggerMetadata.Properties != null && triggerMetadata.Properties.TryGetValue(nameof(AzureComponentFactory), out object value))
            {
                azureComponentFactory = value as AzureComponentFactory;
            }
            else
            {
                azureComponentFactory = serviceProvider.GetService<AzureComponentFactory>();
            }

            IConfiguration config = serviceProvider.GetService<IConfiguration>();
            ILoggerFactory loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            CosmosDbMetadata cosmosDbMetadata = JsonConvert.DeserializeObject<CosmosDbMetadata>(triggerMetadata.Metadata.ToString());
            cosmosDbMetadata.ResolveProperties(serviceProvider.GetService<INameResolver>());
            ICosmosDBServiceFactory serviceFactory = new DefaultCosmosDBServiceFactory(config, azureComponentFactory);
            CosmosClient cosmosClient = serviceFactory.CreateService(cosmosDbMetadata.Connection, new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway
            });
            var monitoredContainer = cosmosClient.GetContainer(cosmosDbMetadata.DatabaseName, cosmosDbMetadata.ContainerName);
            var leaseContainer = cosmosClient.GetContainer(string.IsNullOrEmpty(cosmosDbMetadata.LeaseDatabaseName) ? cosmosDbMetadata.DatabaseName : cosmosDbMetadata.LeaseDatabaseName, string.IsNullOrEmpty(cosmosDbMetadata.LeaseContainerName) ? CosmosDBTriggerConstants.DefaultLeaseCollectionName : cosmosDbMetadata.LeaseContainerName);
            _scaleMonitor = new CosmosDBScaleMonitor(triggerMetadata.FunctionName, loggerFactory.CreateLogger<CosmosDBScaleMonitor>(), monitoredContainer, leaseContainer, cosmosDbMetadata.LeaseContainerPrefix);
            _targetScaler = new CosmosDBTargetScaler(triggerMetadata.FunctionName, cosmosDbMetadata.MaxItemsPerInvocation, monitoredContainer, leaseContainer, cosmosDbMetadata.LeaseContainerPrefix, loggerFactory.CreateLogger<CosmosDBTargetScaler>());
        }

        public IScaleMonitor GetMonitor()
        {
            return _scaleMonitor;
        }

        public ITargetScaler GetTargetScaler()
        {
            return _targetScaler;
        }

        internal class CosmosDbMetadata
        {
            [JsonProperty]
            public string Connection { get; set; }

            [JsonProperty]
            public string DatabaseName { get; set; }

            [JsonProperty]
            public string ContainerName { get; set; }

            [JsonProperty]
            public string LeaseContainerName { get; set; }

            [JsonProperty]
            public string LeaseContainerPrefix { get; set; }

            [JsonProperty]            
            public string LeaseDatabaseName { get; set; }

            [JsonProperty]
            public int MaxItemsPerInvocation { get; set; }

            public void ResolveProperties(INameResolver resolver)
            {
                if (resolver != null)
                {
                    DatabaseName = resolver.ResolveWholeString(DatabaseName);
                    ContainerName = resolver.ResolveWholeString(ContainerName);
                    LeaseContainerName = resolver.ResolveWholeString(LeaseContainerName);
                    LeaseContainerPrefix = resolver.ResolveWholeString(LeaseContainerPrefix) ?? string.Empty;
                    LeaseDatabaseName = resolver.ResolveWholeString(LeaseDatabaseName);
                }
            }
        }
    }
}
