// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger
{
    internal class CosmosDBMetricsProvider
    {
        private readonly ILogger _logger;
        private readonly Container _monitoredContainer;
        private readonly Container _leaseContainer;
        private readonly string _processorName;
        private readonly int _maxAssignWorkerOnNotFoundCount = 5;
        private int _assignWorkerOnNotFoundCount = 0;

        public CosmosDBMetricsProvider(ILogger logger, Container monitoredContainer, Container leaseContainer, string processorName)
        {
            _logger = logger;
            _monitoredContainer = monitoredContainer;
            _leaseContainer = leaseContainer;
            _processorName = processorName;
        }

        public async Task<CosmosDBTriggerMetrics> GetMetricsAsync()
        {
            int partitionCount = 0;
            long remainingWork = 0;

            try
            {
                List<ChangeFeedProcessorState> partitionWorkList = new List<ChangeFeedProcessorState>();
                ChangeFeedEstimator estimator = _monitoredContainer.GetChangeFeedEstimator(_processorName, _leaseContainer);
                using (FeedIterator<ChangeFeedProcessorState> iterator = estimator.GetCurrentStateIterator())
                {
                    while (iterator.HasMoreResults)
                    {
                        FeedResponse<ChangeFeedProcessorState> response = await iterator.ReadNextAsync();
                        partitionWorkList.AddRange(response);
                    }
                }

                partitionCount = partitionWorkList.Count;
                if (partitionCount == 0)
                {
                    partitionCount = 1;
                    remainingWork = 1;
                    _logger.LogWarning(Events.OnScaling, "PartitionCount is 0, the lease container exists but it has not been initialized, scale out to 1 and wait for the first execution.");
                }
                else
                {
                    remainingWork = partitionWorkList.Sum(item => item.EstimatedLag);
                }
            }
            catch (CosmosException cosmosException) when (cosmosException.StatusCode == HttpStatusCode.Gone)
            {
                // Temporary handling of split issue described in https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4285
                // This happens if the main instance is not running, potentially using Consumption Plan
                // In this case, we return a positive value to make the Scale Controller consider spinning at least a single instance
                partitionCount = 1;
                remainingWork = 1;
            }
            catch (CosmosException cosmosException) when (cosmosException.StatusCode == HttpStatusCode.NotFound 
                && _assignWorkerOnNotFoundCount < _maxAssignWorkerOnNotFoundCount)
            {
                // An exception "Not found" may indicate that the lease container does not exist.
                // We vote to scale out, assign a worker to create the lease container.
                // However, it could also signal an issue with the monitoring container configuration.
                // As a result, we make a limited number of attempts to create the lease container.
                _assignWorkerOnNotFoundCount++;
                _logger.LogWarning(Events.OnScaling, $"Possible non-exiting lease container detected. Trying to create the lease container, attempt '{_assignWorkerOnNotFoundCount}'",
                    cosmosException.GetType().ToString(), cosmosException.Message);
                partitionCount = 1;
                remainingWork = 1;
            }

            return new CosmosDBTriggerMetrics
            {
                Timestamp = DateTime.UtcNow,
                PartitionCount = partitionCount,
                RemainingWork = remainingWork
            };
        }
    }
}
