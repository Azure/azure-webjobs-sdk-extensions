// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger
{
    public class CosmosDBScaleMonitor : IScaleMonitor<CosmosDBTriggerMetrics>
    {
        private readonly ILogger _logger;
        private readonly Container _monitoredContainer;
        private readonly Container _leaseContainer;
        private readonly string _processorName;

        // Todo should be in the shared class.
        private static readonly Dictionary<string, string> KnownDocumentClientErrors = new Dictionary<string, string>()
        {
            { "Resource Not Found", "Please check that the CosmosDB container and leases container exist and are listed correctly in Functions config files." },
            { "The input authorization token can't serve the request", string.Empty },
            { "The MAC signature found in the HTTP request is not the same", string.Empty },
            { "Service is currently unavailable.", string.Empty },
            { "Entity with the specified id does not exist in the system.", string.Empty },
            { "Subscription owning the database account is disabled.", string.Empty },
            { "Request rate is large", string.Empty },
            { "PartitionKey value must be supplied for this operation.", "We do not support lease containers with partitions at this time. Please create a new lease collection without partitions." },
            { "The remote name could not be resolved:", string.Empty },
            { "Owner resource does not exist", string.Empty },
            { "The specified document collection is invalid", string.Empty }
        };

        public CosmosDBScaleMonitor(ILogger logger, Container monitoredContainer, Container leaseContainer, string processorName, ScaleMonitorDescriptor scaleMonitorDescriptor)
        {
            _logger = logger;
            _monitoredContainer = monitoredContainer;
            _leaseContainer = leaseContainer;
            _processorName = processorName;
            Descriptor = scaleMonitorDescriptor;
        }

        public ScaleMonitorDescriptor Descriptor { get; set; }

        public async Task<CosmosDBTriggerMetrics> GetMetricsAsync()
        {
            int partitionCount = 0;
            long remainingWork = 0;

            try
            {
                List<ChangeFeedProcessorState> partitionWorkList = new List<ChangeFeedProcessorState>();
                ChangeFeedEstimator estimator = this._monitoredContainer.GetChangeFeedEstimator(this._processorName, this._leaseContainer);
                using (FeedIterator<ChangeFeedProcessorState> iterator = estimator.GetCurrentStateIterator())
                {
                    while (iterator.HasMoreResults)
                    {
                        FeedResponse<ChangeFeedProcessorState> response = await iterator.ReadNextAsync();
                        partitionWorkList.AddRange(response);
                    }
                }

                partitionCount = partitionWorkList.Count;
                remainingWork = partitionWorkList.Sum(item => item.EstimatedLag);
            }
            catch (Exception e) when (e is CosmosException || e is InvalidOperationException)
            {
                if (!TryHandleCosmosException(e))
                {
                    _logger.LogWarning(Events.OnScaling, "Unable to handle {0}: {1}", e.GetType().ToString(), e.Message);
                    if (e is InvalidOperationException)
                    {
                        throw;
                    }
                }
            }
            catch (System.Net.Http.HttpRequestException e)
            {
                string errormsg;

                var webException = e.InnerException as WebException;
                if (webException != null &&
                    webException.Status == WebExceptionStatus.ProtocolError)
                {
                    string statusCode = ((HttpWebResponse)webException.Response).StatusCode.ToString();
                    string statusDesc = ((HttpWebResponse)webException.Response).StatusDescription;
                    errormsg = string.Format("CosmosDBTrigger status {0}: {1}.", statusCode, statusDesc);
                }
                else if (webException != null &&
                    webException.Status == WebExceptionStatus.NameResolutionFailure)
                {
                    errormsg = string.Format("CosmosDBTrigger Exception message: {0}.", webException.Message);
                }
                else
                {
                    errormsg = e.ToString();
                }

                _logger.LogWarning(Events.OnScaling, errormsg);
            }

            return new CosmosDBTriggerMetrics
            {
                Timestamp = DateTime.UtcNow,
                PartitionCount = partitionCount,
                RemainingWork = remainingWork
            };
        }

        private ScaleStatus GetScaleStatus(ScaleStatusContext<CosmosDBTriggerMetrics> context)
        {
            return GetScaleStatusCore(context.WorkerCount, context.Metrics?.Cast<CosmosDBTriggerMetrics>().ToArray());
        }

        public ScaleStatus GetScaleStatus(ScaleStatusContext context)
        {
            return GetScaleStatusCore(context.WorkerCount, context.Metrics?.Cast<CosmosDBTriggerMetrics>().ToArray());
        }

        async Task<ScaleMetrics> IScaleMonitor.GetMetricsAsync()
        {
            return await GetMetricsAsync();
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
                                                     $"of partitions for container ({this._monitoredContainer.Id}, {partitionCount})."));
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
                _logger.LogInformation(Events.OnScaling, string.Format($"Remaining work for container ({this._monitoredContainer.Id}, {latestRemainingWork}) " +
                                                     $"is too high relative to the number of instances ({workerCount})."));
                return status;
            }

            bool documentsWaiting = metrics.All(m => m.RemainingWork > 0);
            if (documentsWaiting && partitionCount > 0 && partitionCount > workerCount)
            {
                status.Vote = ScaleVote.ScaleOut;
                _logger.LogInformation(Events.OnScaling, string.Format($"CosmosDB container '{this._monitoredContainer.Id}' has documents waiting to be processed."));
                _logger.LogInformation(Events.OnScaling, string.Format($"There are {workerCount} instances relative to {partitionCount} partitions."));
                return status;
            }

            // Check to see if the trigger source has been empty for a while. Only if all trigger sources are empty do we scale down.
            bool isIdle = metrics.All(m => m.RemainingWork == 0);
            if (isIdle)
            {
                status.Vote = ScaleVote.ScaleIn;
                _logger.LogInformation(Events.OnScaling, string.Format($"'{this._monitoredContainer.Id}' is idle."));
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
                _logger.LogInformation(Events.OnScaling, $"Remaining work is increasing for '{this._monitoredContainer.Id}'.");
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
                _logger.LogInformation(Events.OnScaling, $"Remaining work is decreasing for '{this._monitoredContainer.Id}'.");
                return status;
            }

            _logger.LogInformation(Events.OnScaling, $"CosmosDB container '{this._monitoredContainer.Id}' is steady.");

            return status;
        }

        // TODO The following section should be shared code
        private bool TryHandleCosmosException(Exception exception)
        {
            string errormsg = null;
            string exceptionMessage = exception.Message;

            if (!string.IsNullOrEmpty(exceptionMessage))
            {
                foreach (KeyValuePair<string, string> exceptionString in KnownDocumentClientErrors)
                {
                    if (exceptionMessage.IndexOf(exceptionString.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        errormsg = !string.IsNullOrEmpty(exceptionString.Value) ? exceptionString.Value : exceptionMessage;
                    }
                }
            }

            if (!string.IsNullOrEmpty(errormsg))
            {
                _logger.LogWarning(errormsg);
                return true;
            }

            return false;
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

        ScaleStatus IScaleMonitor<CosmosDBTriggerMetrics>.GetScaleStatus(ScaleStatusContext<CosmosDBTriggerMetrics> context)
        {
            return GetScaleStatusCore(context.WorkerCount, context.Metrics?.Cast<CosmosDBTriggerMetrics>().ToArray());
        }
    }
}
