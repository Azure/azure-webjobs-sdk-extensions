// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.ChangeFeedProcessor;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.WebJobs.Host.Executors;
    using Microsoft.Azure.WebJobs.Host.Listeners;

    internal class CosmosDBTriggerListener : IListener, IChangeFeedObserverFactory
    {
        private readonly ITriggeredFunctionExecutor executor;
        private readonly ChangeFeedEventHost host;
        public CosmosDBTriggerListener(ITriggeredFunctionExecutor executor, DocumentCollectionInfo documentCollectionLocation, DocumentCollectionInfo leaseCollectionLocation, ChangeFeedHostOptions leaseHostOptions)
        {
            this.executor = executor;
            string hostName = Guid.NewGuid().ToString();
            this.host = new ChangeFeedEventHost(hostName, documentCollectionLocation, leaseCollectionLocation, new ChangeFeedOptions(), leaseHostOptions);
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

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return host.RegisterObserverFactoryAsync(this);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return host.UnregisterObserversAsync();
        }
    }
}
