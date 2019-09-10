// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.ChangeFeedProcessor;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBTriggerBinding : ITriggerBinding
    {
        private readonly ParameterInfo _parameter;
        private readonly DocumentCollectionInfo _documentCollectionLocation;
        private readonly DocumentCollectionInfo _leaseCollectionLocation;
        private readonly ChangeFeedProcessorOptions _processorOptions;
        private readonly ILogger _logger;
        private readonly IReadOnlyDictionary<string, Type> _emptyBindingContract = new Dictionary<string, Type>();
        private readonly IReadOnlyDictionary<string, object> _emptyBindingData = new Dictionary<string, object>();
        private readonly ICosmosDBService _monitoredCosmosDBService;
        private readonly ICosmosDBService _leasesCosmosDBService;

        public CosmosDBTriggerBinding(ParameterInfo parameter, 
            DocumentCollectionInfo documentCollectionLocation, 
            DocumentCollectionInfo leaseCollectionLocation, 
            ChangeFeedProcessorOptions processorOptions, 
            ICosmosDBService monitoredCosmosDBService,
            ICosmosDBService leasesCosmosDBService,
            ILogger logger)
        {
            _documentCollectionLocation = documentCollectionLocation;
            _leaseCollectionLocation = leaseCollectionLocation;
            _processorOptions = processorOptions;
            _parameter = parameter;
            _logger = logger;
            _monitoredCosmosDBService = monitoredCosmosDBService;
            _leasesCosmosDBService = leasesCosmosDBService;
        }

        /// <summary>
        /// Gets the type of the value the Trigger receives from the Executor.
        /// </summary>
        public Type TriggerValueType => typeof(IReadOnlyList<Document>);

        internal DocumentCollectionInfo DocumentCollectionLocation => _documentCollectionLocation;

        internal DocumentCollectionInfo LeaseCollectionLocation => _leaseCollectionLocation;

        internal ChangeFeedProcessorOptions ChangeFeedProcessorOptions => _processorOptions;

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _emptyBindingContract; }
        }

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            // ValueProvider is via binding rules. 
            return Task.FromResult<ITriggerData>(new TriggerData(null, _emptyBindingData));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context", "Missing listener context");
            }

            return Task.FromResult<IListener>(new CosmosDBTriggerListener(
                context.Executor,
                context.Descriptor.Id,
                this._documentCollectionLocation,
                this._leaseCollectionLocation,
                this._processorOptions,
                this._monitoredCosmosDBService,
                this._leasesCosmosDBService,
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
                CollectionName = this._documentCollectionLocation.CollectionName
            };
        }

        internal static bool TryAndConvertToDocumentList(object value, out IReadOnlyList<Document> documents)
        {
            documents = null;

            try
            {
                if (value is IReadOnlyList<Document> docs)
                {
                    documents = docs;
                }
                else if (value is string stringVal)
                {
                    documents = JsonConvert.DeserializeObject<IReadOnlyList<Document>>(stringVal);
                }

                return documents != null;
            }
            catch
            {
                return false;
            }
        }
    }
}