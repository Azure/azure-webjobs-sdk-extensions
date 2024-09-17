// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger
{
    internal class CosmosDBMetricsProvider
    {
        private readonly ILogger _logger;
        private readonly Container _monitoredContainer;
        private readonly Container _leaseContainer;
        private readonly string _processorName;
        private readonly int _maxAssignWorkerOnNotFoundCount = 5;
        private int _assignWorkerOnNotFoundCount = 0;

        private static readonly Dictionary<string, string> KnownDocumentClientErrors = new Dictionary<string, string>()
        {
            { "Resource Not Found", "Please check that the CosmosDB container and leases container exist and are listed correctly in Functions config files." },
            { "The input authorization token can't serve the request", string.Empty },
            { "The MAC signature found in the HTTP request is not the same", string.Empty },
            { "Service is currently unavailable.", string.Empty },
            { "Entity with the specified id does not exist in the system.", string.Empty },
            { "Subscription owning the database account is disabled.", string.Empty },
            { "Request rate is large", string.Empty },
            { "The remote name could not be resolved:", string.Empty },
            { "Owner resource does not exist", string.Empty },
            { "The specified document collection is invalid", string.Empty }
        };

        public CosmosDBMetricsProvider(ILogger logger, Container monitoredContainer, Container leaseContainer, string processorName)
        {
            _logger = logger;
            _monitoredContainer = monitoredContainer;
            _leaseContainer = leaseContainer;
            _processorName = processorName;
        }

        public async Task<CosmosDBTriggerMetrics> GetMetricsAsync()
        {
            int partitionCount = 0;
            long remainingWork = 0;

            try
            {
                List<ChangeFeedProcessorState> partitionWorkList = new List<ChangeFeedProcessorState>();
                ChangeFeedEstimator estimator = _monitoredContainer.GetChangeFeedEstimator(_processorName, _leaseContainer);
                using (FeedIterator<ChangeFeedProcessorState> iterator = estimator.GetCurrentStateIterator())
                {
                    while (iterator.HasMoreResults)
                    {
                        FeedResponse<ChangeFeedProcessorState> response = await iterator.ReadNextAsync();
                        partitionWorkList.AddRange(response);
                    }
                }

                partitionCount = partitionWorkList.Count;
                if (partitionCount == 0)
                {
                    partitionCount = 1;
                    remainingWork = 1;
                    _logger.LogWarning(Events.OnScaling, "PartitionCount is 0, the lease container exists but it has not been initialized, scale out to 1 and wait for the first execution.");
                }
                else
                {
                    remainingWork = partitionWorkList.Sum(item => item.EstimatedLag);
                }
            }
            catch (CosmosException cosmosException) when (cosmosException.StatusCode == HttpStatusCode.Gone)
            {
                // Temporary handling of split issue described in https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4285
                // This happens if the main instance is not running, potentially using Consumption Plan
                // In this case, we return a positive value to make the Scale Controller consider spinning at least a single instance
                partitionCount = 1;
                remainingWork = 1;
            }
            catch (CosmosException cosmosException) when (cosmosException.StatusCode == HttpStatusCode.NotFound 
                && _assignWorkerOnNotFoundCount < _maxAssignWorkerOnNotFoundCount)
            {
                // An exception "Not found" may indicate that the lease container does not exist.
                // We vote to scale out, assign a worker to create the lease container.
                // However, it could also signal an issue with the monitoring container configuration.
                // As a result, we make a limited number of attempts to create the lease container.
                _assignWorkerOnNotFoundCount++;
                _logger.LogWarning(Events.OnScaling, $"Possible non-exiting lease container detected. Trying to create the lease container, attempt '{_assignWorkerOnNotFoundCount}'",
                    cosmosException.GetType().ToString(), cosmosException.Message);
                partitionCount = 1;
                remainingWork = 1;
            }
            catch (Exception e) when (e is CosmosException || e is InvalidOperationException)
            {
                if (!TryHandleCosmosException(e))
                {
                    _logger.LogWarning(Events.OnScaling, "Unable to handle {0}: {1}", e.GetType().ToString(), e.Message);
                    if (e is InvalidOperationException)
                    {
                        throw;
                    }
                }
            }
            catch (System.Net.Http.HttpRequestException e)
            {
                string errormsg;

                var webException = e.InnerException as WebException;
                if (webException != null &&
                    webException.Status == WebExceptionStatus.ProtocolError)
                {
                    string statusCode = ((HttpWebResponse)webException.Response).StatusCode.ToString();
                    string statusDesc = ((HttpWebResponse)webException.Response).StatusDescription;
                    errormsg = string.Format("CosmosDBTrigger status {0}: {1}.", statusCode, statusDesc);
                }
                else if (webException != null &&
                    webException.Status == WebExceptionStatus.NameResolutionFailure)
                {
                    errormsg = string.Format("CosmosDBTrigger Exception message: {0}.", webException.Message);
                }
                else
                {
                    errormsg = e.ToString();
                }

                _logger.LogWarning(Events.OnScaling, errormsg);
            }
            catch (Exception e)
            {
                _logger.LogWarning(Events.OnScaling, "Exception occurred while obtaining metrics for CosmosDB {0}: {1}.", e.GetType().ToString(), e.Message);
            }

            return new CosmosDBTriggerMetrics
            {
                Timestamp = DateTime.UtcNow,
                PartitionCount = partitionCount,
                RemainingWork = remainingWork
            };
        }

        // Since all exceptions in the Cosmos client are thrown as CosmosExceptions, we have to parse their error strings because we dont have access to the internal types
        private bool TryHandleCosmosException(Exception exception)
        {
            string errormsg = null;
            string exceptionMessage = exception.Message;

            if (!string.IsNullOrEmpty(exceptionMessage))
            {
                foreach (KeyValuePair<string, string> exceptionString in KnownDocumentClientErrors)
                {
                    if (exceptionMessage.IndexOf(exceptionString.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        errormsg = !string.IsNullOrEmpty(exceptionString.Value) ? exceptionString.Value : exceptionMessage;
                    }
                }
            }

            if (!string.IsNullOrEmpty(errormsg))
            {
                _logger.LogWarning(errormsg);
                return true;
            }

            return false;
        }
    }
}
