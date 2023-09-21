// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    [SupportsRetry]
    internal class CosmosDBTriggerBinding<T> : ITriggerBinding
    {
        private static readonly IReadOnlyDictionary<string, Type> _emptyBindingContract = new Dictionary<string, Type>();
        private static readonly IReadOnlyDictionary<string, object> _emptyBindingData = new Dictionary<string, object>();
        private readonly ParameterInfo _parameter;
        private readonly string _processorName;
        private readonly ILogger _logger;
        private readonly Container _monitoredContainer;
        private readonly Container _leaseContainer;
        private readonly CosmosDBTriggerAttribute _cosmosDBAttribute;
        private readonly IDrainModeManager _drainModeManager;

        public CosmosDBTriggerBinding(
            ParameterInfo parameter, 
            string processorName,
            Container monitoredContainer,
            Container leaseContainer,
            CosmosDBTriggerAttribute cosmosDBAttribute,
            IDrainModeManager drainModeManager,
            ILogger logger)
        {
            _monitoredContainer = monitoredContainer;
            _leaseContainer = leaseContainer;
            _cosmosDBAttribute = cosmosDBAttribute;
            _parameter = parameter;
            _processorName = processorName;
            _drainModeManager = drainModeManager;
            _logger = logger;
        }

        /// <summary>
        /// Gets the type of the value the Trigger receives from the Executor.
        /// </summary>
        public Type TriggerValueType
        {
            get
            {
                return typeof(IReadOnlyCollection<T>);
            }
        }

        internal Container MonitoredContainer => _monitoredContainer;

        internal Container LeaseContainer => _leaseContainer;

        internal string ProcessorName => _processorName;

        internal CosmosDBTriggerAttribute CosmosDBAttribute => _cosmosDBAttribute;

        public IReadOnlyDictionary<string, Type> BindingDataContract => CosmosDBTriggerBinding<T>._emptyBindingContract;

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            IValueProvider valueBinder = new CosmosDBTriggerValueBinder(_parameter, value);
            return Task.FromResult<ITriggerData>(new TriggerData(valueBinder, _emptyBindingData));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context", "Missing listener context");
            }

            return Task.FromResult<IListener>(new CosmosDBTriggerListener<T>(
                context.Executor,
                context.Descriptor.Id,
                this._processorName,
                this._monitoredContainer, 
                this._leaseContainer, 
                this._cosmosDBAttribute,
                this._drainModeManager,
                this._logger));
        }

        /// <summary>
        /// Shows display information on the dashboard.
        /// </summary>
        /// <returns></returns>
        public ParameterDescriptor ToParameterDescriptor()
        {
            return new CosmosDBTriggerParameterDescriptor
            {
                Name = _parameter.Name,
                Type = CosmosDBTriggerConstants.TriggerName,
                CollectionName = this._monitoredContainer.Id
            };
        }
    }
}