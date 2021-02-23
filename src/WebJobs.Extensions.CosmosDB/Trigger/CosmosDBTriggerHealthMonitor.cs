// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.Documents.ChangeFeedProcessor.Exceptions;
using Microsoft.Azure.Documents.ChangeFeedProcessor.Monitoring;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerHealthMonitor : IHealthMonitor
    {
        private readonly ILogger _logger;

        public CosmosDBTriggerHealthMonitor(ILogger logger)
        {
            this._logger = logger;
        }

        public Task InspectAsync(HealthMonitoringRecord record)
        {
            switch (record.Severity)
            {
                case HealthSeverity.Critical:
                    this._logger.LogCritical(record.Exception, $"Critical error detected in the operation {record.Operation} for {record.Lease}.");
                    break;
                case HealthSeverity.Error:
                    if (record.Exception is LeaseLostException)
                    {
                        this._logger.LogWarning(record.Exception, $"Lease was lost during operation {record.Operation} for {record.Lease}. This is expected during scaling and briefly during initialization as the leases are rebalanced across instances.");
                    }
                    else
                    {
                        this._logger.LogError(record.Exception, $"{record.Operation} encountered an error for {record.Lease}.");
                    }
                    break;
                default:
                    this._logger.LogTrace($"{record.Operation} on lease {record.Lease}.");
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
