// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Linq;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger
{
    internal class CosmosDBScalerFactory : IScalerFactory
    {
        private AzureComponentFactory _componentFactory;

        public CosmosDBScalerFactory(AzureComponentFactory componentFactory)
        {
            _componentFactory = componentFactory;
        }

        public IScaleMonitor CreateScalerMonitor(ScalerContext context)
        {
            (string functionId, Container container, Container leaseContainer, CosmosDBTriggerAttribute attribute, string leaseContainerPrefix, ILoggerFactory loggerFactory) = CreateParameters(context);
            ILogger logger = loggerFactory.CreateLogger<CosmosDBScaleMonitor>();
            return new CosmosDBScaleMonitor(functionId, container, leaseContainer, leaseContainerPrefix, logger);
        }

        public ITargetScaler CreateTargetScaler(ScalerContext context)
        {
            (string functionId, Container container, Container leaseContainer, CosmosDBTriggerAttribute attribute, string leaseContainerPrefix, ILoggerFactory loggerFactory) = CreateParameters(context);
            ILogger logger = loggerFactory.CreateLogger<CosmosDBTargetScaler>();

            return new CosmosDBTargetScaler(functionId, attribute, container, leaseContainer, leaseContainerPrefix, logger);
        }

        private (string FunctionId, Container Container, Container LeaseContainer, CosmosDBTriggerAttribute CosmosDBTriggerAttribute, string LeaseContainerPrefix, ILoggerFactory LoggerFactory) CreateParameters(ScalerContext scalerContext)
        {
            DefaultCosmosDBServiceFactory serviceFactory = new DefaultCosmosDBServiceFactory(scalerContext.Configration, _componentFactory);
            INameResolver resolver = new DefaultNameResolver(scalerContext.Configration);

            TriggerData[] allTriggers = JsonConvert.DeserializeObject<TriggerData[]>(scalerContext.TriggerData);
            TriggerData targetTrigger = allTriggers.SingleOrDefault(x => x.FunctionName == scalerContext.FunctionId);
            targetTrigger = ValidateAndProcessTriggerData(targetTrigger);

            CosmosClientOptions options = new CosmosClientOptions();
            CosmosDBTriggerAttribute attribute = new CosmosDBTriggerAttribute(targetTrigger.DatabaseName, targetTrigger.ContainerName) { MaxItemsPerInvocation = targetTrigger.MaxItemsPerInvocation };

            Container container = serviceFactory.CreateService(targetTrigger.Connection, options).GetContainer(targetTrigger.DatabaseName, targetTrigger.ContainerName);
            Container leaseContainer = serviceFactory.CreateService(targetTrigger.LeaseConnection, options).GetContainer(targetTrigger.DatabaseName, targetTrigger.LeaseContainerName);

            string functionId = targetTrigger.FunctionName;
            string leaseContainerPrefix = targetTrigger.LeaseContainerPrefix;
            ILoggerFactory loggerFactory = scalerContext.LoggerFactory;

            return (FunctionId: functionId, Container: container, LeaseContainer: leaseContainer, CosmosDBTriggerAttribute: attribute, LeaseContainerPrefix: leaseContainerPrefix, LoggerFactory: loggerFactory);
        }

        private TriggerData ValidateAndProcessTriggerData(TriggerData triggerData)
        {
            // Validate required arguments
            if (triggerData == null)
            {
                throw new ArgumentNullException(nameof(triggerData));
            }
            else if (string.IsNullOrEmpty(triggerData.Connection))
            {
                throw new ArgumentNullException(nameof(triggerData.Connection));
            }
            else if (string.IsNullOrEmpty(triggerData.DatabaseName))
            {
                throw new ArgumentNullException(nameof(triggerData.DatabaseName));
            }
            else if (string.IsNullOrEmpty(triggerData.ContainerName))
            {
                throw new ArgumentNullException(nameof(triggerData.ContainerName));
            }

            // Populate optional arguments
            triggerData.LeaseConnection = triggerData.LeaseConnection ?? triggerData.Connection;
            triggerData.LeaseContainerName = triggerData.LeaseContainerName ?? "leases";
            triggerData.LeaseContainerPrefix = triggerData.LeaseContainerPrefix ?? string.Empty;

            return triggerData;
        }

        // Taken from: https://github.com/Azure/azure-webjobs-sdk-extensions/blob/dev/src/WebJobs.Extensions.CosmosDB/Trigger/CosmosDBTriggerAttribute.cs
        // Extracting only the attributes necessary to make scale decisions.
        // Assumes the attributes have already been resolved if defined as an environment variable
        [JsonObject]
        private class TriggerData
        {
            [JsonProperty]
            public string FunctionName { get; set; }

            [JsonProperty]
            public string DatabaseName { get; set; }

            [JsonProperty]
            public string Connection { get; set; }

            [JsonProperty]
            public string ContainerName { get; set; }

            [JsonProperty]
            public string LeaseConnection { get; set; }

            [JsonProperty]
            public string LeaseContainerName { get; set; }

            [JsonProperty]
            public int MaxItemsPerInvocation { get; set; }

            [JsonProperty]
            public string LeaseContainerPrefix { get; set; }
        }
    }
}
