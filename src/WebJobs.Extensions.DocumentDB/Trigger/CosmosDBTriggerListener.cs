// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.ChangeFeedProcessor;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.WebJobs.Host;
    using Microsoft.Azure.WebJobs.Host.Executors;
    using Microsoft.Azure.WebJobs.Host.Listeners;

    internal class CosmosDBTriggerListener : IListener, IChangeFeedObserverFactory
    {
        private const int ListenerNotRegistered = 0;
        private const int ListenerRegistering = 1;
        private const int ListenerRegistered = 2;

        private readonly ITriggeredFunctionExecutor executor;
        private readonly TraceWriter trace;
        private readonly DocumentCollectionInfo monitorCollection;
        private readonly DocumentCollectionInfo leaseCollection;
        private readonly string hostName;
        private readonly ChangeFeedOptions changeFeedOptions;
        private readonly ChangeFeedHostOptions leaseHostOptions;
        private ChangeFeedEventHost host;
        private int _listenerStatus;

        public CosmosDBTriggerListener(ITriggeredFunctionExecutor executor, DocumentCollectionInfo documentCollectionLocation, DocumentCollectionInfo leaseCollectionLocation, ChangeFeedHostOptions leaseHostOptions, ChangeFeedOptions changeFeedOptions, TraceWriter trace)
        {
            this.trace = trace;
            this.executor = executor;
            this.hostName = Guid.NewGuid().ToString();

            this.monitorCollection = documentCollectionLocation;
            this.leaseCollection = leaseCollectionLocation;
            this.leaseHostOptions = leaseHostOptions;
            this.changeFeedOptions = changeFeedOptions;
        }

        public void Cancel()
        {
            this.StopAsync(CancellationToken.None).Wait();
        }

        public IChangeFeedObserver CreateObserver()
        {
            return new CosmosDBTriggerObserver(this.executor);
        }

        public void Dispose()
        {
            //Nothing to dispose
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref this._listenerStatus, ListenerRegistering, ListenerNotRegistered) != ListenerNotRegistered)
            {
                throw new InvalidOperationException("The listener has already been started or is starting.");
            }

            this.InitializeHost();

            try
            {
                await this.host.RegisterObserverFactoryAsync(this);
                Interlocked.CompareExchange(ref this._listenerStatus, ListenerRegistered, ListenerRegistering);
            }
            catch (DocumentClientException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Throw a custom error so that it's easier to decipher.
                string message = $"Either the source collection '{monitorCollection.CollectionName}' (in database '{monitorCollection.DatabaseName}')  or the lease collection '{leaseCollection.CollectionName}' (in database '{leaseCollection.DatabaseName}') does not exist. Both collections must exist before the listener starts. To automatically create the lease collection, set '{nameof(CosmosDBTriggerAttribute.CreateLeaseCollectionIfNotExists)}' to 'true'.";
                this.host = null;
                throw new InvalidOperationException(message, ex);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (this.host != null)
                {
                    await this.host.UnregisterObserversAsync();
                    this._listenerStatus = ListenerNotRegistered;
                }
            }
            catch (Exception ex)
            {
                this.trace.Warning($"Stopping the observer failed, potentially it was never started. Exception: {ex.Message}.");
            }
        }

        private void InitializeHost()
        {
            if (this.host == null)
            {
                this.host = new ChangeFeedEventHost(this.hostName, this.monitorCollection, this.leaseCollection, this.changeFeedOptions, leaseHostOptions);
            }
        }
    }
}
