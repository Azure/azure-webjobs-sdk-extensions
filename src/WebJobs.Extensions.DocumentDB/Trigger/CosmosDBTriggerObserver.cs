// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.ChangeFeedProcessor;
    using Microsoft.Azure.WebJobs.Host.Executors;

    internal class CosmosDBTriggerObserver : IChangeFeedObserver
    {
        private readonly ITriggeredFunctionExecutor executor;

        public CosmosDBTriggerObserver(ITriggeredFunctionExecutor executor)
        {
            this.executor = executor;
        }
        public Task CloseAsync(ChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context", "Missing observer context");
            }
            return Task.CompletedTask;
        }
        public Task OpenAsync(ChangeFeedObserverContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context", "Missing observer context");
            }
            return Task.CompletedTask;
        }

        public Task ProcessChangesAsync(ChangeFeedObserverContext context, IReadOnlyList<Document> docs)
        {
            return this.executor.TryExecuteAsync(new TriggeredFunctionData() { TriggerValue = docs }, CancellationToken.None);
        }
    }
}
