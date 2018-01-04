// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.ChangeFeedProcessor;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerObserver : IChangeFeedObserver
    {
        private readonly ITriggeredFunctionExecutor executor;
        private readonly int retryCount;

        public CosmosDBTriggerObserver(ITriggeredFunctionExecutor executor, int retryCount)
        {
            this.executor = executor;
            this.retryCount = retryCount;
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

        public async Task ProcessChangesAsync(ChangeFeedObserverContext context, IReadOnlyList<Document> docs)
        {
            int retries = 0;

            while (true)
            {
                var result = await this.executor.TryExecuteAsync(new TriggeredFunctionData() { TriggerValue = docs }, CancellationToken.None);

                if (result.Succeeded)
                {
                    // notification processed successfully
                    return;
                }

                if (retryCount != -1 && retries >= retryCount)
                {
                    // Function invocation failed, but no retries are left
                    return;
                }

                // Function invocation failed, but retries are left, or infinite retries was specified
                retries++;
                continue;
            }
        }
    }
}
