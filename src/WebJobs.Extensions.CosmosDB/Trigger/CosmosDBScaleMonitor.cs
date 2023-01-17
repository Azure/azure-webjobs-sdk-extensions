// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger
{
    /// <summary>
    /// Scale Monitor class for a CosmosDB function.
    /// </summary>
    public class CosmosDBScaleMonitor : IScaleMonitor<CosmosDBTriggerMetrics>
    {
        private readonly ILogger _logger;
        private readonly string _functionId;
        private readonly Container _monitoredContainer;
        private readonly ScaleMonitorDescriptor _scaleMonitorDescriptor;
        private readonly CosmosDBMetricsProvider _cosmosDBMetricsProvider;

        /// <summary>
        /// Instantiates a scale monitor for CosmosDB function.
        /// </summary>
        /// <param name="functionId">FunctionId of the monitored function.</param>
        /// <param name="logger">Used for logging.</param>
        /// <param name="monitoredContainer">Monitored container for CosmosDB function.</param>
        /// <param name="leaseContainer">Lease container for CosmosDB function.</param>
        /// <param name="processorName">Processor name used for function.</param>
        public CosmosDBScaleMonitor(string functionId, ILogger logger, Container monitoredContainer, Container leaseContainer, string processorName)
        {
            _logger = logger;
            _functionId = functionId;
            _monitoredContainer = monitoredContainer;
            _scaleMonitorDescriptor = new ScaleMonitorDescriptor($"{_functionId}-CosmosDBTrigger-{_monitoredContainer.Database.Id}-{_monitoredContainer.Id}".ToLower(), _functionId);
            _cosmosDBMetricsProvider = new CosmosDBMetricsProvider(logger, monitoredContainer, leaseContainer, processorName);
        }

        public ScaleMonitorDescriptor Descriptor => _scaleMonitorDescriptor;

        public ScaleStatus GetScaleStatus(ScaleStatusContext<CosmosDBTriggerMetrics> context)
        {
            return GetScaleStatusCore(context.WorkerCount, context.Metrics?.ToArray());
        }

        async Task<ScaleMetrics> IScaleMonitor.GetMetricsAsync()
        {
            return await GetMetricsAsync().ConfigureAwait(false);
        }

        public async Task<CosmosDBTriggerMetrics> GetMetricsAsync()
        {
            return await _cosmosDBMetricsProvider.GetMetricsAsync().ConfigureAwait(false);
        }

        public ScaleStatus GetScaleStatus(ScaleStatusContext context)
        {
            context.Metrics?.Cast<CosmosDBTriggerMetrics>().ToArray();

            return GetScaleStatusCore(context.WorkerCount, context.Metrics?.Cast<CosmosDBTriggerMetrics>().ToArray());
        }

        private ScaleStatus GetScaleStatusCore(int workerCount, CosmosDBTriggerMetrics[] metrics)
        {
            ScaleStatus status = new ScaleStatus
            {
                Vote = ScaleVote.None
            };

            const int NumberOfSamplesToConsider = 5;

            // Unable to determine the correct vote with no metrics.
            if (metrics == null)
            {
                return status;
            }

            // We shouldn't assign more workers than there are partitions (Cosmos DB, Event Hub, Service Bus Queue/Topic)
            // This check is first, because it is independent of load or number of samples.
            int partitionCount = metrics.Length > 0 ? metrics.Last().PartitionCount : 0;
            if (partitionCount > 0 && partitionCount < workerCount)
            {
                status.Vote = ScaleVote.ScaleIn;
                _logger.LogInformation(Events.OnScaling, string.Format($"WorkerCount ({workerCount}) > PartitionCount ({partitionCount})."));
                _logger.LogInformation(Events.OnScaling, string.Format($"Number of instances ({workerCount}) is too high relative to number " +
                                                     $"of partitions for container ({_monitoredContainer.Id}, {partitionCount})."));
                return status;
            }

            // At least 5 samples are required to make a scale decision for the rest of the checks.
            if (metrics.Length < NumberOfSamplesToConsider)
            {
                return status;
            }

            // Maintain a minimum ratio of 1 worker per 1,000 items of remaining work.
            long latestRemainingWork = metrics.Last().RemainingWork;
            if (latestRemainingWork > workerCount * 1000)
            {
                status.Vote = ScaleVote.ScaleOut;
                _logger.LogInformation(Events.OnScaling, string.Format($"RemainingWork ({latestRemainingWork}) > WorkerCount ({workerCount}) * 1,000."));
                _logger.LogInformation(Events.OnScaling, string.Format($"Remaining work for container ({_monitoredContainer.Id}, {latestRemainingWork}) " +
                                                     $"is too high relative to the number of instances ({workerCount})."));
                return status;
            }

            bool documentsWaiting = metrics.All(m => m.RemainingWork > 0);
            if (documentsWaiting && partitionCount > 0 && partitionCount > workerCount)
            {
                status.Vote = ScaleVote.ScaleOut;
                _logger.LogInformation(Events.OnScaling, string.Format($"CosmosDB container '{_monitoredContainer.Id}' has documents waiting to be processed."));
                _logger.LogInformation(Events.OnScaling, string.Format($"There are {workerCount} instances relative to {partitionCount} partitions."));
                return status;
            }

            // Check to see if the trigger source has been empty for a while. Only if all trigger sources are empty do we scale down.
            bool isIdle = metrics.All(m => m.RemainingWork == 0);
            if (isIdle)
            {
                status.Vote = ScaleVote.ScaleIn;
                _logger.LogInformation(Events.OnScaling, string.Format($"'{_monitoredContainer.Id}' is idle."));
                return status;
            }

            // Samples are in chronological order. Check for a continuous increase in work remaining.
            // If detected, this results in an automatic scale out for the site container.
            bool remainingWorkIncreasing =
                IsTrueForLast(
                    metrics,
                    NumberOfSamplesToConsider,
                    (prev, next) => prev.RemainingWork < next.RemainingWork) && metrics[0].RemainingWork > 0;
            if (remainingWorkIncreasing)
            {
                status.Vote = ScaleVote.ScaleOut;
                _logger.LogInformation(Events.OnScaling, $"Remaining work is increasing for '{_monitoredContainer.Id}'.");
                return status;
            }

            bool remainingWorkDecreasing =
                IsTrueForLast(
                    metrics,
                    NumberOfSamplesToConsider,
                    (prev, next) => prev.RemainingWork > next.RemainingWork);
            if (remainingWorkDecreasing)
            {
                status.Vote = ScaleVote.ScaleIn;
                _logger.LogInformation(Events.OnScaling, $"Remaining work is decreasing for '{_monitoredContainer.Id}'.");
                return status;
            }

            _logger.LogInformation(Events.OnScaling, $"CosmosDB container '{_monitoredContainer.Id}' is steady.");

            return status;
        }

        private static bool IsTrueForLast(IList<CosmosDBTriggerMetrics> metrics, int count, Func<CosmosDBTriggerMetrics, CosmosDBTriggerMetrics, bool> predicate)
        {
            Debug.Assert(count > 1, "count must be greater than 1.");
            Debug.Assert(count <= metrics.Count, "count must be less than or equal to the list size.");

            // Walks through the list from left to right starting at len(samples) - count.
            for (int i = metrics.Count - count; i < metrics.Count - 1; i++)
            {
                if (!predicate(metrics[i], metrics[i + 1]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
