// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerListener<T> : IListener, IScaleMonitor<CosmosDBTriggerMetrics>
    {
        private const int ListenerNotRegistered = 0;
        private const int ListenerRegistering = 1;
        private const int ListenerRegistered = 2;

        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ILogger _logger;
        private readonly Container _monitoredContainer;
        private readonly Container _leaseContainer;
        private readonly CosmosDBTriggerAttribute _cosmosDBAttribute;
        private readonly string _hostName;
        private readonly string _processorName;
        private readonly string _functionId;
        private readonly ScaleMonitorDescriptor _scaleMonitorDescriptor;
        private ChangeFeedProcessor _host;
        private ChangeFeedProcessorBuilder _hostBuilder;
        private int _listenerStatus;

        private static readonly Dictionary<string, string> KnownDocumentClientErrors = new Dictionary<string, string>()
        {
            { "Resource Not Found", "Please check that the CosmosDB collection and leases collection exist and are listed correctly in Functions config files." },
            { "The input authorization token can't serve the request", string.Empty },
            { "The MAC signature found in the HTTP request is not the same", string.Empty },
            { "Service is currently unavailable.", string.Empty },
            { "Entity with the specified id does not exist in the system.", string.Empty },
            { "Subscription owning the database account is disabled.", string.Empty },
            { "Request rate is large", string.Empty },
            { "PartitionKey value must be supplied for this operation.", "We do not support lease collections with partitions at this time. Please create a new lease collection without partitions." },
            { "The remote name could not be resolved:", string.Empty },
            { "Owner resource does not exist", string.Empty },
            { "The specified document collection is invalid", string.Empty }
        };

        public CosmosDBTriggerListener(
            ITriggeredFunctionExecutor executor,
            string functionId,
            string processorName,
            Container monitoredContainer,
            Container leaseContainer,
            CosmosDBTriggerAttribute cosmosDBAttribute,
            ILogger logger)
        {
            this._logger = logger;
            this._executor = executor;
            this._processorName = processorName;
            this._hostName = Guid.NewGuid().ToString();
            this._functionId = functionId;
            this._monitoredContainer = monitoredContainer;
            this._leaseContainer = leaseContainer;
            this._cosmosDBAttribute = cosmosDBAttribute;
            this._scaleMonitorDescriptor = new ScaleMonitorDescriptor($"{_functionId}-CosmosDBTrigger-{_monitoredContainer.Database.Id}-{_monitoredContainer.Id}".ToLower());
        }

        public ScaleMonitorDescriptor Descriptor => this._scaleMonitorDescriptor;

        public void Cancel()
        {
            this.StopAsync(CancellationToken.None).Wait();
        }

        public void Dispose()
        {
            //Nothing to dispose
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            int previousStatus = Interlocked.CompareExchange(ref this._listenerStatus, ListenerRegistering, ListenerNotRegistered);

            if (previousStatus == ListenerRegistering)
            {
                throw new InvalidOperationException("The listener is already starting.");
            }
            else if (previousStatus == ListenerRegistered)
            {
                throw new InvalidOperationException("The listener has already started.");
            }

            this.InitializeBuilder();

            try
            {
                await this.StartProcessorAsync();
                Interlocked.CompareExchange(ref this._listenerStatus, ListenerRegistered, ListenerRegistering);
            }
            catch (Exception ex)
            {
                // Reset to NotRegistered
                this._listenerStatus = ListenerNotRegistered;

                // Throw a custom error if NotFound.
                if (ex is CosmosException docEx && docEx.StatusCode == HttpStatusCode.NotFound)
                {
                    // Throw a custom error so that it's easier to decipher.
                    string message = $"Either the source collection '{this._cosmosDBAttribute.CollectionName}' (in database '{this._cosmosDBAttribute.DatabaseName}')  or the lease collection '{this._cosmosDBAttribute.LeaseCollectionName}' (in database '{this._cosmosDBAttribute.LeaseDatabaseName}') does not exist. Both collections must exist before the listener starts. To automatically create the lease collection, set '{nameof(CosmosDBTriggerAttribute.CreateLeaseCollectionIfNotExists)}' to 'true'.";
                    this._host = null;
                    throw new InvalidOperationException(message, ex);
                }

                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (this._host != null)
                {
                    await this._host.StopAsync().ConfigureAwait(false);
                    this._listenerStatus = ListenerNotRegistered;
                }
            }
            catch (Exception ex)
            {
                this._logger.LogWarning($"Stopping the observer failed, potentially it was never started. Exception: {ex.Message}.");
            }
        }

        internal virtual Task StartProcessorAsync()
        {
            if (this._host == null)
            {
                this._host = this._hostBuilder.Build();
            }

            return this._host.StartAsync();
        }

        internal virtual void InitializeBuilder()
        {
            if (this._hostBuilder == null)
            {
                this._hostBuilder = this._monitoredContainer.GetChangeFeedProcessorBuilder<T>(this._processorName, this.ProcessChangesAsync)
                    .WithInstanceName(this._hostName)
                    .WithLeaseContainer(this._leaseContainer);

                if (this._cosmosDBAttribute.MaxItemsPerInvocation > 0)
                {
                    this._hostBuilder.WithMaxItems(this._cosmosDBAttribute.MaxItemsPerInvocation);
                }

                if (!string.IsNullOrEmpty(this._cosmosDBAttribute.StartFromTime))
                {
                    if (this._cosmosDBAttribute.StartFromBeginning)
                    {
                        throw new InvalidOperationException("Only one of StartFromBeginning or StartFromTime can be used");
                    }

                    if (!DateTime.TryParse(this._cosmosDBAttribute.StartFromTime, out DateTime startFromTime))
                    {
                        throw new InvalidOperationException(@"The specified StartFromTime parameter is not in the correct format. Please use the ISO 8601 format with the UTC designator. For example: '2021-02-16T14:19:29Z'.");
                    }

                    this._hostBuilder.WithStartTime(startFromTime);
                }
                else
                {
                    if (this._cosmosDBAttribute.StartFromBeginning)
                    {
                        this._hostBuilder.WithStartTime(DateTime.MinValue.ToUniversalTime());
                    }
                }

                if (this._cosmosDBAttribute.FeedPollDelay > 0)
                {
                    this._hostBuilder.WithPollInterval(TimeSpan.FromMilliseconds(this._cosmosDBAttribute.FeedPollDelay));
                }

                TimeSpan? leaseAcquireInterval = null;
                if (this._cosmosDBAttribute.LeaseAcquireInterval > 0)
                {
                    leaseAcquireInterval = TimeSpan.FromMilliseconds(this._cosmosDBAttribute.LeaseAcquireInterval);
                }

                TimeSpan? leaseExpirationInterval = null;
                if (this._cosmosDBAttribute.LeaseExpirationInterval > 0)
                {
                    leaseExpirationInterval = TimeSpan.FromMilliseconds(this._cosmosDBAttribute.LeaseExpirationInterval);
                }

                TimeSpan? leaseRenewInterval = null;
                if (this._cosmosDBAttribute.LeaseRenewInterval > 0)
                {
                    leaseRenewInterval = TimeSpan.FromMilliseconds(this._cosmosDBAttribute.LeaseRenewInterval);
                }

                this._hostBuilder.WithLeaseConfiguration(leaseAcquireInterval, leaseExpirationInterval, leaseRenewInterval);
            }
        }

        private Task ProcessChangesAsync(IReadOnlyCollection<T> docs, CancellationToken cancellationToken)
        {
            return this._executor.TryExecuteAsync(new TriggeredFunctionData() { TriggerValue = docs }, cancellationToken);
        }

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
                    _logger.LogWarning("Unable to handle {0}: {1}", e.GetType().ToString(), e.Message);
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

                _logger.LogWarning(errormsg);
            }

            return new CosmosDBTriggerMetrics
            {
                Timestamp = DateTime.UtcNow,
                PartitionCount = partitionCount,
                RemainingWork = remainingWork
            };
        }

        public ScaleStatus GetScaleStatus(ScaleStatusContext<CosmosDBTriggerMetrics> context)
        {
            return GetScaleStatusCore(context.WorkerCount, context.Metrics?.Cast<CosmosDBTriggerMetrics>().ToArray());
        }

        async Task<ScaleMetrics> IScaleMonitor.GetMetricsAsync()
        {
            return await GetMetricsAsync();
        }

        public ScaleStatus GetScaleStatus(ScaleStatusContext context)
        {
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
                _logger.LogInformation(string.Format($"WorkerCount ({workerCount}) > PartitionCount ({partitionCount})."));
                _logger.LogInformation(string.Format($"Number of instances ({workerCount}) is too high relative to number " +
                                                     $"of partitions for collection ({this._monitoredContainer.Id}, {partitionCount})."));
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
                _logger.LogInformation(string.Format($"RemainingWork ({latestRemainingWork}) > WorkerCount ({workerCount}) * 1,000."));
                _logger.LogInformation(string.Format($"Remaining work for collection ({this._monitoredContainer.Id}, {latestRemainingWork}) " +
                                                     $"is too high relative to the number of instances ({workerCount})."));
                return status;
            }

            bool documentsWaiting = metrics.All(m => m.RemainingWork > 0);
            if (documentsWaiting && partitionCount > 0 && partitionCount > workerCount)
            {
                status.Vote = ScaleVote.ScaleOut;
                _logger.LogInformation(string.Format($"CosmosDB collection '{this._monitoredContainer.Id}' has documents waiting to be processed."));
                _logger.LogInformation(string.Format($"There are {workerCount} instances relative to {partitionCount} partitions."));
                return status;
            }

            // Check to see if the trigger source has been empty for a while. Only if all trigger sources are empty do we scale down.
            bool isIdle = metrics.All(m => m.RemainingWork == 0);
            if (isIdle)
            {
                status.Vote = ScaleVote.ScaleIn;
                _logger.LogInformation(string.Format($"'{this._monitoredContainer.Id}' is idle."));
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
                _logger.LogInformation($"Remaining work is increasing for '{this._monitoredContainer.Id}'.");
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
                _logger.LogInformation($"Remaining work is decreasing for '{this._monitoredContainer.Id}'.");
                return status;
            }

            _logger.LogInformation($"CosmosDB collection '{this._monitoredContainer.Id}' is steady.");

            return status;
        }

        // Since all exceptions in the Cosmos client are thrown as CosmosExceptions, we have to parse their error strings because we dont have access to the internal types
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
    }
}
