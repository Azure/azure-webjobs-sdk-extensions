// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.ChangeFeedProcessor;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerListener : IListener, IChangeFeedObserverFactory
    {
        private const int ListenerNotRegistered = 0;
        private const int ListenerRegistering = 1;
        private const int ListenerRegistered = 2;

        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ILogger _logger;
        private readonly DocumentCollectionInfo _monitorCollection;
        private readonly DocumentCollectionInfo _leaseCollection;
        private readonly string _hostName;
        private readonly ChangeFeedOptions _changeFeedOptions;
        private readonly ChangeFeedHostOptions _leaseHostOptions;
        private readonly int _retryCount;
        private ChangeFeedEventHost _host;
        private int _listenerStatus;

        public CosmosDBTriggerListener(ITriggeredFunctionExecutor executor, DocumentCollectionInfo documentCollectionLocation, DocumentCollectionInfo leaseCollectionLocation, ChangeFeedHostOptions leaseHostOptions, ChangeFeedOptions changeFeedOptions, ILogger logger, int retryCount)
        {
            this._logger = logger;
            this._executor = executor;
            this._hostName = Guid.NewGuid().ToString();
            this._monitorCollection = documentCollectionLocation;
            this._leaseCollection = leaseCollectionLocation;
            this._leaseHostOptions = leaseHostOptions;
            this._changeFeedOptions = changeFeedOptions;
            this._retryCount = retryCount;
        }

        public void Cancel()
        {
            this.StopAsync(CancellationToken.None).Wait();
        }

        public IChangeFeedObserver CreateObserver()
        {
            return new CosmosDBTriggerObserver(this._executor, this._retryCount);
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

            this.InitializeHost();

            try
            {
                await RegisterObserverFactoryAsync();
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

        // For test mocking
        internal virtual Task RegisterObserverFactoryAsync()
        {
            return this._host.RegisterObserverFactoryAsync(this);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (this._host != null)
                {
                    await this._host.UnregisterObserversAsync();
                    this._listenerStatus = ListenerNotRegistered;
                }
            }
            catch (Exception ex)
            {
                this._logger.LogWarning($"Stopping the observer failed, potentially it was never started. Exception: {ex.Message}.");
            }
        }

        private void InitializeHost()
        {
            if (this._host == null)
            {
                this._host = new ChangeFeedEventHost(this._hostName,
                    this._monitorCollection,
                    this._leaseCollection,
                    this._changeFeedOptions,
                    this._leaseHostOptions);
            }
        }
    }
}
