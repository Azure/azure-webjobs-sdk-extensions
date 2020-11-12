// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.ChangeFeedProcessor;
using Microsoft.Azure.Documents.ChangeFeedProcessor.Monitoring;
using Microsoft.Azure.Documents.ChangeFeedProcessor.PartitionManagement;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerListener : IListener, IScaleMonitor<CosmosDBTriggerMetrics>, Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing.IChangeFeedObserverFactory
    {
        private const int ListenerNotRegistered = 0;
        private const int ListenerRegistering = 1;
        private const int ListenerRegistered = 2;

        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ILogger _logger;
        private readonly DocumentCollectionInfo _monitorCollection;
        private readonly DocumentCollectionInfo _leaseCollection;
        private readonly string _hostName;
        private readonly ChangeFeedProcessorOptions _processorOptions;
        private readonly ICosmosDBService _monitoredCosmosDBService;
        private readonly ICosmosDBService _leasesCosmosDBService;
        private readonly IHealthMonitor _healthMonitor;
        private IChangeFeedProcessor _host;
        private ChangeFeedProcessorBuilder _hostBuilder;
        private ChangeFeedProcessorBuilder _workEstimatorBuilder;
        private IRemainingWorkEstimator _workEstimator;
        private int _listenerStatus;
        private string _functionId;
        private ScaleMonitorDescriptor _scaleMonitorDescriptor;

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

        public CosmosDBTriggerListener(ITriggeredFunctionExecutor executor,
            string functionId,
            DocumentCollectionInfo documentCollectionLocation,
            DocumentCollectionInfo leaseCollectionLocation,
            ChangeFeedProcessorOptions processorOptions,
            ICosmosDBService monitoredCosmosDBService,
            ICosmosDBService leasesCosmosDBService,
            ILogger logger,
            IRemainingWorkEstimator workEstimator = null)
        {
            this._logger = logger;
            this._executor = executor;
            this._functionId = functionId;
            this._hostName = Guid.NewGuid().ToString();

            this._monitorCollection = documentCollectionLocation;
            this._leaseCollection = leaseCollectionLocation;
            this._processorOptions = processorOptions;
            this._monitoredCosmosDBService = monitoredCosmosDBService;
            this._leasesCosmosDBService = leasesCosmosDBService;
            this._healthMonitor = new CosmosDBTriggerHealthMonitor(this._logger);

            this._workEstimator = workEstimator;

            this._scaleMonitorDescriptor = new ScaleMonitorDescriptor($"{_functionId}-CosmosDBTrigger-{_monitorCollection.DatabaseName}-{_monitorCollection.CollectionName}".ToLower());
        }

        public ScaleMonitorDescriptor Descriptor
        {
            get
            {
                return _scaleMonitorDescriptor;
            }
        }

        public void Cancel()
        {
            this.StopAsync(CancellationToken.None).Wait();
        }

        public Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing.IChangeFeedObserver CreateObserver()
        {
            return new CosmosDBTriggerObserver(this._executor);
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
                if (ex is DocumentClientException docEx && docEx.StatusCode == HttpStatusCode.NotFound)
                {
                    // Throw a custom error so that it's easier to decipher.
                    string message = $"Either the source collection '{_monitorCollection.CollectionName}' (in database '{_monitorCollection.DatabaseName}')  or the lease collection '{_leaseCollection.CollectionName}' (in database '{_leaseCollection.DatabaseName}') does not exist. Both collections must exist before the listener starts. To automatically create the lease collection, set '{nameof(CosmosDBTriggerAttribute.CreateLeaseCollectionIfNotExists)}' to 'true'.";
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

        internal virtual async Task StartProcessorAsync()
        {
            if (this._host == null)
            {
                this._host = await this._hostBuilder.BuildAsync().ConfigureAwait(false);
            }

            await this._host.StartAsync().ConfigureAwait(false);
        }

        private void InitializeBuilder()
        {
            if (this._hostBuilder == null)
            {
                this._hostBuilder = new ChangeFeedProcessorBuilder()
                    .WithHostName(this._hostName)
                    .WithFeedDocumentClient(this._monitoredCosmosDBService.GetClient())
                    .WithLeaseDocumentClient(this._leasesCosmosDBService.GetClient())
                    .WithFeedCollection(this._monitorCollection)
                    .WithLeaseCollection(this._leaseCollection)
                    .WithProcessorOptions(this._processorOptions)
                    .WithHealthMonitor(this._healthMonitor)
                    .WithObserverFactory(this);
            }
        }

        private async Task<IRemainingWorkEstimator> GetWorkEstimatorAsync()
        {
            if (_workEstimatorBuilder == null)
            {
                _workEstimatorBuilder = new ChangeFeedProcessorBuilder()
                    .WithHostName(this._hostName)
                    .WithFeedDocumentClient(this._monitoredCosmosDBService.GetClient())
                    .WithLeaseDocumentClient(this._leasesCosmosDBService.GetClient())
                    .WithFeedCollection(this._monitorCollection)
                    .WithLeaseCollection(this._leaseCollection)
                    .WithProcessorOptions(this._processorOptions)
                    .WithHealthMonitor(this._healthMonitor)
                    .WithObserverFactory(this);
            }

            if (_workEstimator == null)
            {
                _workEstimator = await _workEstimatorBuilder.BuildEstimatorAsync();
            }

            return _workEstimator;
        }

        async Task<ScaleMetrics> IScaleMonitor.GetMetricsAsync()
        {
            return await GetMetricsAsync();
        }

        public async Task<CosmosDBTriggerMetrics> GetMetricsAsync()
        {
            int partitionCount = 0;
            long remainingWork = 0;
            IReadOnlyList<RemainingPartitionWork> partitionWorkList = null;

            try
            {
                IRemainingWorkEstimator workEstimator = await GetWorkEstimatorAsync();
                partitionWorkList = await workEstimator.GetEstimatedRemainingWorkPerPartitionAsync();

                partitionCount = partitionWorkList.Count;
                remainingWork = partitionWorkList.Sum(item => item.RemainingWork);
            }
            catch (Exception e) when (e is DocumentClientException || e is InvalidOperationException)
            {
                if (!TryHandleDocumentClientException(e))
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

        ScaleStatus IScaleMonitor.GetScaleStatus(ScaleStatusContext context)
        {
            return GetScaleStatusCore(context.WorkerCount, context.Metrics?.Cast<CosmosDBTriggerMetrics>().ToArray());
        }

        public ScaleStatus GetScaleStatus(ScaleStatusContext<CosmosDBTriggerMetrics> context)
        {
            return GetScaleStatusCore(context.WorkerCount, context.Metrics?.ToArray());
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
                                                     $"of partitions for collection ({this._monitorCollection.CollectionName}, {partitionCount})."));
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
                _logger.LogInformation(string.Format($"Remaining work for collection ({this._monitorCollection.CollectionName}, {latestRemainingWork}) " +
                                                     $"is too high relative to the number of instances ({workerCount})."));
                return status;
            }

            bool documentsWaiting = metrics.All(m => m.RemainingWork > 0);
            if (documentsWaiting && partitionCount > 0 && partitionCount > workerCount)
            {
                status.Vote = ScaleVote.ScaleOut;
                _logger.LogInformation(string.Format($"CosmosDB collection '{this._monitorCollection.CollectionName}' has documents waiting to be processed."));
                _logger.LogInformation(string.Format($"There are {workerCount} instances relative to {partitionCount} partitions."));
                return status;
            }

            // Check to see if the trigger source has been empty for a while. Only if all trigger sources are empty do we scale down.
            bool isIdle = metrics.All(m => m.RemainingWork == 0);
            if (isIdle)
            {
                status.Vote = ScaleVote.ScaleIn;
                _logger.LogInformation(string.Format($"'{this._monitorCollection.CollectionName}' is idle."));
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
                _logger.LogInformation($"Remaining work is increasing for '{this._monitorCollection.CollectionName}'.");
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
                _logger.LogInformation($"Remaining work is decreasing for '{this._monitorCollection.CollectionName}'.");
                return status;
            }

            _logger.LogInformation($"CosmosDB collection '{this._monitorCollection.CollectionName}' is steady.");

            return status;
        }

        // Since all exceptions in the Document client are thrown as DocumentClientExceptions, we have to parse their error strings because we dont have access to the internal types
        // In the form Microsoft.Azure.Documents.DocumentClientException or Microsoft.Azure.Documents.UnauthorizedException
        private bool TryHandleDocumentClientException(Exception exception)
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
