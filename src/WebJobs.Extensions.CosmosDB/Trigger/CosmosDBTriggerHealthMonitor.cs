// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
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
                    this._logger.LogCritical($"Unhealthiness detected in the operation {record.Operation} for {record.Lease}. ", record.Exception);
                    break;
                case HealthSeverity.Error:
                    this._logger.LogError($"Unhealthiness detected in the operation {record.Operation} for {record.Lease}. ", record.Exception);
                    break;
                default:
                    this._logger.LogTrace($"{record.Operation} on lease {record.Lease?.Id}, partition {record.Lease?.PartitionId} for owner {record.Lease?.Owner}");
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
