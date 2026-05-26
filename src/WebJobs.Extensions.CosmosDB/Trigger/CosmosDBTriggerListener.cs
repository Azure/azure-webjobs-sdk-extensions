// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger;
using Microsoft.Azure.WebJobs.Host;
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
        private readonly CancellationTokenSource _functionExecutionCancellationTokenSource;
        private readonly IDrainModeManager _drainModeManager;
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
            IDrainModeManager drainModeManager,
            ILogger logger)
        {
            _logger = logger;
            _executor = executor;
            _drainModeManager = drainModeManager;
            _functionExecutionCancellationTokenSource = new CancellationTokenSource();
            _processorName = processorName;
            _hostName = Guid.NewGuid().ToString();
            _functionId = functionId;
            _monitoredContainer = monitoredContainer;
            _leaseContainer = leaseContainer;
            _cosmosDBAttribute = cosmosDBAttribute;
            _scaleMonitorDescriptor = new ScaleMonitorDescriptor($"{_functionId}-CosmosDBTrigger-{_monitoredContainer.Database.Id}-{_monitoredContainer.Id}".ToLower());
            _healthMonitor = new CosmosDBTriggerHealthMonitor(logger);
            _listenerLogDetails = $"prefix='{_processorName}', monitoredContainer='{_monitoredContainer.Id}', monitoredDatabase='{_monitoredContainer.Database.Id}', " +
                $"leaseContainer='{_leaseContainer.Id}', leaseDatabase='{_leaseContainer.Database.Id}', functionId='{_functionId}'";

            _cosmosDBScaleMonitor = new CosmosDBScaleMonitor(_functionId, logger, _monitoredContainer, _leaseContainer, _processorName);
            _cosmosDBTargetScaler = new CosmosDBTargetScaler(_functionId, _cosmosDBAttribute.MaxItemsPerInvocation, _monitoredContainer, _leaseContainer, _processorName, _logger);
        }

        public ScaleMonitorDescriptor Descriptor => _scaleMonitorDescriptor;

        public void Cancel()
        {
            StopAsync(CancellationToken.None).Wait();
        }

        public void Dispose()
        {
            _functionExecutionCancellationTokenSource.Cancel();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            int previousStatus = Interlocked.CompareExchange(ref _listenerStatus, ListenerRegistering, ListenerNotRegistered);

            if (previousStatus == ListenerRegistering)
            {
                throw new InvalidOperationException("The listener is already starting.");
            }
            else if (previousStatus == ListenerRegistered)
            {
                throw new InvalidOperationException("The listener has already started.");
            }

            InitializeBuilder();

            try
            {
                await StartProcessorAsync();
                Interlocked.CompareExchange(ref _listenerStatus, ListenerRegistered, ListenerRegistering);
                _logger.LogDebug(Events.OnListenerStarted, "Started the listener for {Details}.", _listenerLogDetails);
            }
            catch (Exception ex)
            {
                // Reset to NotRegistered
                _listenerStatus = ListenerNotRegistered;
                _logger.LogError(Events.OnListenerStartError, "Starting the listener for {Details} failed. Exception: {Exception}.", _listenerLogDetails, ex);

                // Throw a custom error if NotFound.
                if (ex is CosmosException docEx && docEx.StatusCode == HttpStatusCode.NotFound)
                {
                    // Throw a custom error so that it's easier to decipher.
                    string message = $"Either the source container '{_cosmosDBAttribute.ContainerName}' (in database '{_cosmosDBAttribute.DatabaseName}')  or the lease container '{_cosmosDBAttribute.LeaseContainerName}' (in database '{_cosmosDBAttribute.LeaseDatabaseName}') does not exist. Both containers must exist before the listener starts. To automatically create the lease container, set '{nameof(CosmosDBTriggerAttribute.CreateLeaseContainerIfNotExists)}' to 'true'.";
                    _host = null;
                    throw new InvalidOperationException(message, ex);
                }

                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_drainModeManager.IsDrainModeEnabled)
            {
                _functionExecutionCancellationTokenSource.Cancel();
            }

            try
            {
                if (_host != null)
                {
                    await _host.StopAsync().ConfigureAwait(false);
                    _listenerStatus = ListenerNotRegistered;
                    _logger.LogDebug(Events.OnListenerStopped, "Stopped the listener for {Details}.", _listenerLogDetails);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(Events.OnListenerStopError, "Stopping the listener for {Details} failed. Exception: {Exception}.", _listenerLogDetails, ex);
            }
        }

        public IScaleMonitor GetMonitor()
        {
            return _cosmosDBScaleMonitor;
        }

        public ITargetScaler GetTargetScaler()
        {
            return _cosmosDBTargetScaler;
        }

        internal virtual Task StartProcessorAsync()
        {
            _host ??= _hostBuilder.Build();

            return _host.StartAsync();
        }

        internal virtual void InitializeBuilder()
        {
            if (_hostBuilder == null)
            {
                var builder = GetBuilder();
                _hostBuilder = builder
                    .WithErrorNotification(_healthMonitor.OnErrorAsync)
                    .WithLeaseAcquireNotification(_healthMonitor.OnLeaseAcquireAsync)
                    .WithLeaseReleaseNotification(_healthMonitor.OnLeaseReleaseAsync)
                    .WithInstanceName(_hostName)
                    .WithLeaseContainer(_leaseContainer);

                if (_cosmosDBAttribute.MaxItemsPerInvocation > 0)
                {
                    _hostBuilder.WithMaxItems(_cosmosDBAttribute.MaxItemsPerInvocation);
                }

                if (!string.IsNullOrEmpty(_cosmosDBAttribute.StartFromTime))
                {
                    if (_cosmosDBAttribute.StartFromBeginning)
                    {
                        throw new InvalidOperationException("Only one of StartFromBeginning or StartFromTime can be used");
                    }

                    if (!DateTime.TryParse(_cosmosDBAttribute.StartFromTime, out DateTime startFromTime))
                    {
                        throw new InvalidOperationException(@"The specified StartFromTime parameter is not in the correct format. Please use the ISO 8601 format with the UTC designator. For example: '2021-02-16T14:19:29Z'.");
                    }

                    _hostBuilder.WithStartTime(startFromTime);
                }
                else
                {
                    if (_cosmosDBAttribute.StartFromBeginning)
                    {
                        _hostBuilder.WithStartTime(DateTime.MinValue.ToUniversalTime());
                    }
                }

                if (_cosmosDBAttribute.FeedPollDelay > 0)
                {
                    _hostBuilder.WithPollInterval(TimeSpan.FromMilliseconds(_cosmosDBAttribute.FeedPollDelay));
                }

                TimeSpan? leaseAcquireInterval = null;
                if (_cosmosDBAttribute.LeaseAcquireInterval > 0)
                {
                    leaseAcquireInterval = TimeSpan.FromMilliseconds(_cosmosDBAttribute.LeaseAcquireInterval);
                }

                TimeSpan? leaseExpirationInterval = null;
                if (_cosmosDBAttribute.LeaseExpirationInterval > 0)
                {
                    leaseExpirationInterval = TimeSpan.FromMilliseconds(_cosmosDBAttribute.LeaseExpirationInterval);
                }

                TimeSpan? leaseRenewInterval = null;
                if (_cosmosDBAttribute.LeaseRenewInterval > 0)
                {
                    leaseRenewInterval = TimeSpan.FromMilliseconds(_cosmosDBAttribute.LeaseRenewInterval);
                }

                _hostBuilder.WithLeaseConfiguration(leaseAcquireInterval, leaseExpirationInterval, leaseRenewInterval);
            }
        }

        private async Task ProcessChangesAsync<TValue>(ChangeFeedProcessorContext context, IReadOnlyCollection<TValue> docs, CancellationToken cancellationToken)
        {
            // TValue will depend on ChangeFeedMode
            // For LatestVersion, it is T
            // For AllVersionsAndDeletes, it is ChangeFeedItem<T>
            _healthMonitor.OnChangesDelivered(context);
            FunctionResult result = await _executor.TryExecuteAsync(new TriggeredFunctionData() { TriggerValue = docs }, _functionExecutionCancellationTokenSource.Token);
            if (result != null // TryExecuteAsync when using RetryPolicies can return null
                && !result.Succeeded
                && result.Exception != null)
            {
                ChangeFeedProcessorUserException userException = new(result.Exception, context);
                await _healthMonitor.OnErrorAsync(context.LeaseToken, userException);
            }

            // Prevent the change feed lease from being checkpointed if cancellation was requested when not in Drain mode
            _functionExecutionCancellationTokenSource.Token.ThrowIfCancellationRequested();
        }

        private ChangeFeedProcessorBuilder GetBuilder()
        {
            return _cosmosDBAttribute.ChangeFeedMode switch
            {
                CosmosDBChangeFeedMode.LatestVersion => _monitoredContainer.GetChangeFeedProcessorBuilder<T>(_processorName, ProcessChangesAsync),
                CosmosDBChangeFeedMode.AllVersionsAndDeletes => GetAllVersionsAndDeleteBuilder(),
                _ => throw new InvalidOperationException($"Unsupported ChangeFeedMode '{_cosmosDBAttribute.ChangeFeedMode}'"),
            };
        }

        private ChangeFeedProcessorBuilder GetAllVersionsAndDeleteBuilderCore<TInner>()
        {
            return _monitoredContainer.GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes<TInner>(_processorName, ProcessChangesAsync);
        }

        private ChangeFeedProcessorBuilder GetAllVersionsAndDeleteBuilder()
        {
            // We need to unwrap T from ChangeFeedItem<T> to T, and then construct a builder from that.
            // For out-of-process functions the binding-provider generator wraps the default JObject
            // payload into ChangeFeedItem<JObject> before reaching this listener, so by the time
            // we're here T is always ChangeFeedItem<>. Any other T is an in-proc user mistake
            // (e.g. IReadOnlyList<MyDocument> instead of IReadOnlyList<ChangeFeedItem<MyDocument>>);
            // throw early with a clear error rather than silently produce malformed bindings.
            Type itemType = typeof(T);
            if (!itemType.IsGenericType || itemType.GetGenericTypeDefinition() != typeof(ChangeFeedItem<>))
            {
                throw new InvalidOperationException($"When using ChangeFeedMode.AllVersionsAndDeletes, the trigger binding type must be Microsoft.Azure.Cosmos.ChangeFeedItem<T>. Actual type: '{itemType.FullName}'.");
            }

            itemType = itemType.GetGenericArguments()[0];
            MethodInfo method = typeof(CosmosDBTriggerListener<T>).GetMethod(nameof(GetAllVersionsAndDeleteBuilderCore), BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo genericMethod = method.MakeGenericMethod(itemType);
            return (ChangeFeedProcessorBuilder)genericMethod.Invoke(this, null);
        }
    }
}
