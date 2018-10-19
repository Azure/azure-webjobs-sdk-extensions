// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.ChangeFeedProcessor;
using Microsoft.Azure.Documents.ChangeFeedProcessor.Monitoring;
using Microsoft.Azure.Documents.ChangeFeedProcessor.PartitionManagement;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerListener : IListener, Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing.IChangeFeedObserverFactory
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
        private int _listenerStatus;

        public CosmosDBTriggerListener(ITriggeredFunctionExecutor executor,
            DocumentCollectionInfo documentCollectionLocation,
            DocumentCollectionInfo leaseCollectionLocation,
            ChangeFeedProcessorOptions processorOptions,
            ICosmosDBService monitoredCosmosDBService,
            ICosmosDBService leasesCosmosDBService,
            ILogger logger)
        {
            this._logger = logger;
            this._executor = executor;
            this._hostName = Guid.NewGuid().ToString();

            this._monitorCollection = documentCollectionLocation;
            this._leaseCollection = leaseCollectionLocation;
            this._processorOptions = processorOptions;
            this._monitoredCosmosDBService = monitoredCosmosDBService;
            this._leasesCosmosDBService = leasesCosmosDBService;
            this._healthMonitor = new CosmosDBTriggerHealthMonitor(this._logger);
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
    }
}
