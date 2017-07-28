// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
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

    internal class CosmosDBTriggerBinding : ITriggerBinding
    {
        private readonly ParameterInfo _parameter;
        private readonly DocumentCollectionInfo _documentCollectionLocation;
        private readonly DocumentCollectionInfo _leaseCollectionLocation;
        private readonly ChangeFeedHostOptions _leaseHostOptions;
        private readonly IBindingDataProvider _bindingDataProvider;

        public CosmosDBTriggerBinding(ParameterInfo parameter, DocumentCollectionInfo documentCollectionLocation, DocumentCollectionInfo leaseCollectionLocation, ChangeFeedHostOptions leaseHostOptions)
        {
            _bindingDataProvider = BindingDataProvider.FromType(parameter.ParameterType);
            _documentCollectionLocation = documentCollectionLocation;
            _leaseCollectionLocation = leaseCollectionLocation;
            _leaseHostOptions = leaseHostOptions;
            _parameter = parameter;
        }

        /// <summary>
        /// Type of value that the Trigger receives from the Executor
        /// </summary>
        public Type TriggerValueType => typeof(IReadOnlyList<Document>);

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _bindingDataProvider.Contract; }
        }

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            IReadOnlyList<Document> triggerValue;
            try
            {
                triggerValue = value as IReadOnlyList<Document>;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Unable to convert trigger to DocumentDBTrigger:" + ex.Message);
            }

            var valueBinder = new CosmosDBTriggerValueBinder(_parameter.ParameterType, triggerValue);            
            return Task.FromResult<ITriggerData>(new TriggerData(valueBinder, _bindingDataProvider.GetBindingData(valueBinder.GetValue())));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context", "Missing listener context");
            }
            return Task.FromResult<IListener>(new CosmosDBTriggerListener(context.Executor, this._documentCollectionLocation, this._leaseCollectionLocation, this._leaseHostOptions));
        }

        /// <summary>
        /// Shows display information on the dashboard
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
    }
}
