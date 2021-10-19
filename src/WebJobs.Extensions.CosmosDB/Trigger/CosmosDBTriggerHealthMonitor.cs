// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerHealthMonitor
    {
        private readonly ILogger logger;

        public CosmosDBTriggerHealthMonitor(ILogger logger)
        {
            this.logger = logger;
        }

        public Task OnError(string leaseToken, Exception exception)
        {
            switch (exception)
            {
                case ChangeFeedProcessorUserException userException:
                    this.logger.LogWarning(userException.InnerException, "Lease {LeaseToken} encountered an unhandled user exception during processing.", leaseToken);
                    this.logger.LogDebug("Lease {LeaseToken} has error diagnostics {Diagnostics}", leaseToken, userException.ExceptionContext.Diagnostics);
                    break;
                case CosmosException cosmosException when cosmosException.StatusCode == HttpStatusCode.RequestTimeout || cosmosException.StatusCode == HttpStatusCode.ServiceUnavailable:
                    this.logger.LogWarning(cosmosException, "Lease {LeaseToken} experiencing transient connectivity issues.", leaseToken);
                    break;
                default:
                    this.logger.LogError(exception, "Lease {LeaseToken} experienced an error during processing.", leaseToken);
                    break;
            }

            if (exception is CosmosException asCosmosException)
            {
                this.logger.LogDebug("Lease {LeaseToken} has error diagnostics {Diagnostics}", leaseToken, asCosmosException.Diagnostics);
            }

            return Task.CompletedTask;
        }

        public Task OnLeaseAcquire(string leaseToken)
        {
            this.logger.LogInformation("Lease {LeaseToken} was acquired to start processing.", leaseToken);
            return Task.CompletedTask;
        }

        public Task OnLeaseRelease(string leaseToken)
        {
            this.logger.LogInformation("Lease {LeaseToken} was released.", leaseToken);
            return Task.CompletedTask;
        }

        public void OnChangesDelivered(ChangeFeedProcessorContext context)
        {
            this.logger.LogDebug("Events delivered to lease {LeaseToken} consuming {RequestCharge} RU.", context.LeaseToken, context.Headers.RequestCharge);
        }
    }
}
