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
        private readonly ITriggeredFunctionExecutor executor;
        private readonly ChangeFeedEventHost host;
        private readonly TraceWriter trace;
        private readonly DocumentCollectionInfo monitorCollection;
        private readonly DocumentCollectionInfo leaseCollection;

        public CosmosDBTriggerListener(ITriggeredFunctionExecutor executor, DocumentCollectionInfo documentCollectionLocation, DocumentCollectionInfo leaseCollectionLocation, ChangeFeedHostOptions leaseHostOptions, int? maxItemCount, TraceWriter trace)
        {
            this.trace = trace;
            this.executor = executor;
            string hostName = Guid.NewGuid().ToString();

            this.monitorCollection = documentCollectionLocation;
            this.leaseCollection = leaseCollectionLocation;

            ChangeFeedOptions changeFeedOptions = new ChangeFeedOptions();
            if (maxItemCount.HasValue)
            {
                changeFeedOptions.MaxItemCount = maxItemCount;
            }

            this.host = new ChangeFeedEventHost(hostName, documentCollectionLocation, leaseCollectionLocation, changeFeedOptions, leaseHostOptions);
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
            try
            {
                await this.host.RegisterObserverFactoryAsync(this);
            }
            catch (DocumentClientException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Throw a custom error so that it's easier to decipher.
                string message = $"Either the source collection '{monitorCollection.CollectionName}' (in database '{monitorCollection.DatabaseName}')  or the lease collection '{leaseCollection.CollectionName}' (in database '{leaseCollection.DatabaseName}') does not exist. Both collections must exist before the listener starts. To automatically create the lease collection, set '{nameof(CosmosDBTriggerAttribute.CreateLeaseCollectionIfNotExists)}' to 'true'.";
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
                }
            }
            catch (Exception ex)
            {
                this.trace.Error("Stopping the observer failed, potentially it was never started.", ex);
            }
        }
    }
}
