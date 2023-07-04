// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerListener<T> : IListener, IScaleMonitorProvider, ITargetScalerProvider
    {
        private const int ListenerNotRegistered = 0;
        private const int ListenerRegistering = 1;
        private const int ListenerRegistered = 2;

        private readonly ITriggeredFunctionExecutor _executor;
        private readonly ILogger _logger;
        private readonly Container _monitoredContainer;
        private readonly Container _leaseContainer;
        private readonly CosmosDBTriggerAttribute _cosmosDBAttribute;
        private readonly string _hostName;
        private readonly string _processorName;
        private readonly string _functionId;
        private readonly ScaleMonitorDescriptor _scaleMonitorDescriptor;
        private readonly CosmosDBTriggerHealthMonitor _healthMonitor;
        private readonly string _listenerLogDetails;
        private readonly IScaleMonitor<CosmosDBTriggerMetrics> _cosmosDBScaleMonitor;
        private readonly ITargetScaler _cosmosDBTargetScaler;
        private ChangeFeedProcessor _host;
        private ChangeFeedProcessorBuilder _hostBuilder;
        private int _listenerStatus;

        public CosmosDBTriggerListener(
            ITriggeredFunctionExecutor executor,
            string functionId,
            string processorName,
            Container monitoredContainer,
            Container leaseContainer,
            CosmosDBTriggerAttribute cosmosDBAttribute,
            ILogger logger)
        {
            this._logger = logger;
            this._executor = executor;
            this._processorName = processorName;
            this._hostName = Guid.NewGuid().ToString();
            this._functionId = functionId;
            this._monitoredContainer = monitoredContainer;
            this._leaseContainer = leaseContainer;
            this._cosmosDBAttribute = cosmosDBAttribute;
            this._scaleMonitorDescriptor = new ScaleMonitorDescriptor($"{_functionId}-CosmosDBTrigger-{_monitoredContainer.Database.Id}-{_monitoredContainer.Id}".ToLower());
            this._healthMonitor = new CosmosDBTriggerHealthMonitor(logger);
            this._listenerLogDetails = $"prefix='{this._processorName}', monitoredContainer='{this._monitoredContainer.Id}', monitoredDatabase='{this._monitoredContainer.Database.Id}', " +
                $"leaseContainer='{this._leaseContainer.Id}', leaseDatabase='{this._leaseContainer.Database.Id}', functionId='{this._functionId}'";

            this._cosmosDBScaleMonitor = new CosmosDBScaleMonitor(_functionId, logger, _monitoredContainer, _leaseContainer, _processorName);
            this._cosmosDBTargetScaler = new CosmosDBTargetScaler(_functionId, _cosmosDBAttribute.MaxItemsPerInvocation, _monitoredContainer, _leaseContainer, _processorName, _logger);
        }

        public ScaleMonitorDescriptor Descriptor => this._scaleMonitorDescriptor;

        public void Cancel()
        {
            this.StopAsync(CancellationToken.None).Wait();
        }

        public void Dispose()
        {
            //Nothing to dispose
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            int previousStatus = Interlocked.CompareExchange(ref this._listenerStatus, ListenerRegistering, ListenerNotRegistered);

            if (previousStatus == ListenerRegistering)
            {
                throw new InvalidOperationException("The listener is already starting.");
            }
            else if (previousStatus == ListenerRegistered)
            {
                throw new InvalidOperationException("The listener has already started.");
            }

            this.InitializeBuilder();

            try
            {
                await this.StartProcessorAsync();
                Interlocked.CompareExchange(ref this._listenerStatus, ListenerRegistered, ListenerRegistering);
                this._logger.LogDebug(Events.OnListenerStarted, "Started the listener for {Details}.", this._listenerLogDetails);
            }
            catch (Exception ex)
            {
                // Reset to NotRegistered
                this._listenerStatus = ListenerNotRegistered;
                this._logger.LogError(Events.OnListenerStartError, "Starting the listener for {Details} failed. Exception: {Exception}.", this._listenerLogDetails, ex);

                // Throw a custom error if NotFound.
                if (ex is CosmosException docEx && docEx.StatusCode == HttpStatusCode.NotFound)
                {
                    // Throw a custom error so that it's easier to decipher.
                    string message = $"Either the source container '{this._cosmosDBAttribute.ContainerName}' (in database '{this._cosmosDBAttribute.DatabaseName}')  or the lease container '{this._cosmosDBAttribute.LeaseContainerName}' (in database '{this._cosmosDBAttribute.LeaseDatabaseName}') does not exist. Both containers must exist before the listener starts. To automatically create the lease container, set '{nameof(CosmosDBTriggerAttribute.CreateLeaseContainerIfNotExists)}' to 'true'.";
                    this._host = null;
                    throw new InvalidOperationException(message, ex);
                }

                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (this._host != null)
                {
                    await this._host.StopAsync().ConfigureAwait(false);
                    this._listenerStatus = ListenerNotRegistered;
                    this._logger.LogDebug(Events.OnListenerStopped, "Stopped the listener for {Details}.", this._listenerLogDetails);
                }
            }
            catch (Exception ex)
            {
                this._logger.LogError(Events.OnListenerStopError, "Stopping the listener for {Details} failed. Exception: {Exception}.", this._listenerLogDetails, ex);
            }
        }

        internal virtual Task StartProcessorAsync()
        {
            if (this._host == null)
            {
                this._host = this._hostBuilder.Build();
            }

            return this._host.StartAsync();
        }

        internal virtual void InitializeBuilder()
        {
            if (this._hostBuilder == null)
            {
                this._hostBuilder = this._monitoredContainer.GetChangeFeedProcessorBuilder<T>(this._processorName, this.ProcessChangesAsync)
                    .WithErrorNotification(this._healthMonitor.OnErrorAsync)
                    .WithLeaseAcquireNotification(this._healthMonitor.OnLeaseAcquireAsync)
                    .WithLeaseReleaseNotification(this._healthMonitor.OnLeaseReleaseAsync)
                    .WithInstanceName(this._hostName)
                    .WithLeaseContainer(this._leaseContainer);

                if (this._cosmosDBAttribute.MaxItemsPerInvocation > 0)
                {
                    this._hostBuilder.WithMaxItems(this._cosmosDBAttribute.MaxItemsPerInvocation);
                }

                if (!string.IsNullOrEmpty(this._cosmosDBAttribute.StartFromTime))
                {
                    if (this._cosmosDBAttribute.StartFromBeginning)
                    {
                        throw new InvalidOperationException("Only one of StartFromBeginning or StartFromTime can be used");
                    }

                    if (!DateTime.TryParse(this._cosmosDBAttribute.StartFromTime, out DateTime startFromTime))
                    {
                        throw new InvalidOperationException(@"The specified StartFromTime parameter is not in the correct format. Please use the ISO 8601 format with the UTC designator. For example: '2021-02-16T14:19:29Z'.");
                    }

                    this._hostBuilder.WithStartTime(startFromTime);
                }
                else
                {
                    if (this._cosmosDBAttribute.StartFromBeginning)
                    {
                        this._hostBuilder.WithStartTime(DateTime.MinValue.ToUniversalTime());
                    }
                }

                if (this._cosmosDBAttribute.FeedPollDelay > 0)
                {
                    this._hostBuilder.WithPollInterval(TimeSpan.FromMilliseconds(this._cosmosDBAttribute.FeedPollDelay));
                }

                TimeSpan? leaseAcquireInterval = null;
                if (this._cosmosDBAttribute.LeaseAcquireInterval > 0)
                {
                    leaseAcquireInterval = TimeSpan.FromMilliseconds(this._cosmosDBAttribute.LeaseAcquireInterval);
                }

                TimeSpan? leaseExpirationInterval = null;
                if (this._cosmosDBAttribute.LeaseExpirationInterval > 0)
                {
                    leaseExpirationInterval = TimeSpan.FromMilliseconds(this._cosmosDBAttribute.LeaseExpirationInterval);
                }

                TimeSpan? leaseRenewInterval = null;
                if (this._cosmosDBAttribute.LeaseRenewInterval > 0)
                {
                    leaseRenewInterval = TimeSpan.FromMilliseconds(this._cosmosDBAttribute.LeaseRenewInterval);
                }

                this._hostBuilder.WithLeaseConfiguration(leaseAcquireInterval, leaseExpirationInterval, leaseRenewInterval);
            }
        }

        private async Task ProcessChangesAsync(ChangeFeedProcessorContext context, IReadOnlyCollection<T> docs, CancellationToken cancellationToken)
        {
            this._healthMonitor.OnChangesDelivered(context);
            FunctionResult result = await this._executor.TryExecuteAsync(new TriggeredFunctionData() { TriggerValue = docs }, cancellationToken);
            if (result != null // TryExecuteAsync when using RetryPolicies can return null
                && !result.Succeeded
                && result.Exception != null)
            {
                ChangeFeedProcessorUserException userException = new ChangeFeedProcessorUserException(result.Exception, context);
                await this._healthMonitor.OnErrorAsync(context.LeaseToken, userException);
            }
            // Prevent the change feed lease from being checkpointed if cancellation was requested
            cancellationToken.ThrowIfCancellationRequested();
        }

        public IScaleMonitor GetMonitor()
        {
            return _cosmosDBScaleMonitor;
        }

        public ITargetScaler GetTargetScaler()
        {
            return _cosmosDBTargetScaler;
        }
    }
}
