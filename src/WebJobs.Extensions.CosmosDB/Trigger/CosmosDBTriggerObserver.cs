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
        private readonly bool haltOnFailure;

        public CosmosDBTriggerObserver(ITriggeredFunctionExecutor executor, bool haltOnFailure)
        {
            this.executor = executor;
            this.haltOnFailure = haltOnFailure;
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
            // Not sure why these methods aren't declared with the "async" keyword, but wrapping this logic in a task taking an async 
            // lambda so I can use it properly and avoid blocking with a Task.Wait() or Task.Result() call.
            return Task.Run(async () =>
            {
                var result = await this.executor.TryExecuteAsync(new TriggeredFunctionData() { TriggerValue = docs }, CancellationToken.None);

                if (!result.Succeeded && this.haltOnFailure == true)
                {
                    // unfortunate that we can't re-throw and preserve stack trace here
                    throw result.Exception;
                }
            });
        }
    }
}
