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

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerListener : IListener, IChangeFeedObserverFactory
    {
        private readonly ITriggeredFunctionExecutor executor;
        private readonly ChangeFeedEventHost host;
        private readonly DocumentCollectionInfo monitorCollection;
        private readonly DocumentCollectionInfo leaseCollection;
        private readonly int retryCount;

        public CosmosDBTriggerListener(ITriggeredFunctionExecutor executor, DocumentCollectionInfo documentCollectionLocation, DocumentCollectionInfo leaseCollectionLocation, ChangeFeedHostOptions leaseHostOptions, int retryCount)
        {
            this.executor = executor;
            string hostName = Guid.NewGuid().ToString();

            monitorCollection = documentCollectionLocation;
            leaseCollection = leaseCollectionLocation;

            this.host = new ChangeFeedEventHost(hostName, documentCollectionLocation, leaseCollectionLocation, new ChangeFeedOptions(), leaseHostOptions);

            this.retryCount = retryCount;
        }

        public void Cancel()
        {
            this.StopAsync(CancellationToken.None).Wait();
        }

        public IChangeFeedObserver CreateObserver()
        {
            return new CosmosDBTriggerObserver(this.executor, this.retryCount);
        }

        public void Dispose()
        {
            //Nothing to dispose
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await host.RegisterObserverFactoryAsync(this);
            }
            catch (DocumentClientException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Throw a custom error so that it's easier to decipher.
                string message = $"Either the source collection '{monitorCollection.CollectionName}' (in database '{monitorCollection.DatabaseName}')  or the lease collection '{leaseCollection.CollectionName}' (in database '{leaseCollection.DatabaseName}') does not exist. Both collections must exist before the listener starts. To automatically create the lease collection, set '{nameof(CosmosDBTriggerAttribute.CreateLeaseCollectionIfNotExists)}' to 'true'.";
                throw new InvalidOperationException(message, ex);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return host.UnregisterObserversAsync();
        }
    }
}
