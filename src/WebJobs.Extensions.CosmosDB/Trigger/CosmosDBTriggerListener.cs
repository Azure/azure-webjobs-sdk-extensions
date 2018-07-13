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
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ILogger _logger;
        private readonly DocumentCollectionInfo _monitorCollection;
        private readonly DocumentCollectionInfo _leaseCollection;
        private readonly string _hostName;
        private readonly ChangeFeedOptions _changeFeedOptions;
        private readonly ChangeFeedHostOptions _leaseHostOptions;
        private ChangeFeedEventHost _host;
        private bool _listenerStarted = false;

        public CosmosDBTriggerListener(ITriggeredFunctionExecutor executor, DocumentCollectionInfo documentCollectionLocation, DocumentCollectionInfo leaseCollectionLocation, ChangeFeedHostOptions leaseHostOptions, int? maxItemCount, ILogger logger)
        {
            this._logger = logger;
            this._executor = executor;
            this._hostName = Guid.NewGuid().ToString();

            this._monitorCollection = documentCollectionLocation;
            this._leaseCollection = leaseCollectionLocation;
            this._leaseHostOptions = leaseHostOptions;

            ChangeFeedOptions changeFeedOptions = new ChangeFeedOptions();
            if (maxItemCount.HasValue)
            {
                changeFeedOptions.MaxItemCount = maxItemCount;
            }

            this._changeFeedOptions = changeFeedOptions;
        }

        public void Cancel()
        {
            this.StopAsync(CancellationToken.None).Wait();
        }

        public IChangeFeedObserver CreateObserver()
        {
            return new CosmosDBTriggerObserver(this._executor);
        }

        public void Dispose()
        {
            //Nothing to dispose
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (this._listenerStarted)
            {
                throw new InvalidOperationException("The listener has already been started.");
            }

            this.InitializeHost();

            try
            {
                await this._host.RegisterObserverFactoryAsync(this);
                this._listenerStarted = true;
            }
            catch (DocumentClientException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Throw a custom error so that it's easier to decipher.
                string message = $"Either the source collection '{_monitorCollection.CollectionName}' (in database '{_monitorCollection.DatabaseName}')  or the lease collection '{_leaseCollection.CollectionName}' (in database '{_leaseCollection.DatabaseName}') does not exist. Both collections must exist before the listener starts. To automatically create the lease collection, set '{nameof(CosmosDBTriggerAttribute.CreateLeaseCollectionIfNotExists)}' to 'true'.";
                this._host = null;
                throw new InvalidOperationException(message, ex);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (this._host != null && this._listenerStarted)
                {
                    await this._host.UnregisterObserversAsync();
                    this._listenerStarted = false;
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
