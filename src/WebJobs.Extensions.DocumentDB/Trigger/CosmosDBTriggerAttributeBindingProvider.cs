// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    using System;
    using System.Globalization;
    using System.Reflection;
    using System.Threading.Tasks;
    using Config;
    using Microsoft.Azure.Documents.ChangeFeedProcessor;
    using Microsoft.Azure.WebJobs.Host.Triggers;

    internal class CosmosDBTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly string _monitorConnectionString;
        private readonly string _leasesConnectionString;
        private readonly ChangeFeedHostOptions _leasesOptions;

        public CosmosDBTriggerAttributeBindingProvider(string monitorConnectionString, string leasesConnectionString, ChangeFeedHostOptions leasesOptions = null)
        {
            _monitorConnectionString = monitorConnectionString;
            _leasesConnectionString = leasesConnectionString;
            _leasesOptions = leasesOptions ?? new ChangeFeedHostOptions();
        }

        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            // Tries to parse the context parameters and see if it belongs to this [CosmosDBTrigger] binder
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            CosmosDBTriggerAttribute attribute = parameter.GetCustomAttribute<CosmosDBTriggerAttribute>(inherit: false);
            if (attribute == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }
          
            DocumentCollectionInfo documentCollectionLocation;
            DocumentCollectionInfo leaseCollectionLocation;
            ChangeFeedHostOptions leaseHostOptions = ResolveLeaseOptions(attribute);

            try
            {
                string triggerConnectionString = ResolveAttributeConnectionString(attribute);
                DocumentDBConnectionString triggerConnection = new DocumentDBConnectionString(triggerConnectionString);
                if (triggerConnection.ServiceEndpoint == null)
                {
                    throw new InvalidOperationException("The connection string for the monitored collection is in an invalid format, please use AccountEndpoint=XXXXXX;AccountKey=XXXXXX;.");
                }

                DocumentDBConnectionString leasesConnection = new DocumentDBConnectionString(ResolveAttributeLeasesConnectionString(attribute, triggerConnectionString));
                if (triggerConnection.ServiceEndpoint == null)
                {
                    throw new InvalidOperationException("The connection string for the leases collection is in an invalid format, please use AccountEndpoint=XXXXXX;AccountKey=XXXXXX;.");
                }

                documentCollectionLocation = new DocumentCollectionInfo
                {
                    Uri = triggerConnection.ServiceEndpoint,
                    MasterKey = triggerConnection.AuthKey,
                    DatabaseName = attribute.DatabaseName,
                    CollectionName = attribute.CollectionName
                };

                leaseCollectionLocation = new DocumentCollectionInfo
                {
                    Uri = leasesConnection.ServiceEndpoint,
                    MasterKey = leasesConnection.AuthKey,
                    DatabaseName = attribute.LeaseDatabaseName,
                    CollectionName = attribute.LeaseCollectionName
                };

                if (documentCollectionLocation.Uri.Equals(leaseCollectionLocation.Uri)
                    && documentCollectionLocation.DatabaseName.Equals(leaseCollectionLocation.DatabaseName)
                    && documentCollectionLocation.CollectionName.Equals(leaseCollectionLocation.CollectionName))
                {
                    throw new InvalidOperationException("The monitored collection cannot be the same as the collection storing the leases.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format("Cannot create Collection Information for {0} in database {1} with lease {2} in database {3} : {4}", attribute.CollectionName, attribute.DatabaseName, attribute.LeaseCollectionName, attribute.LeaseDatabaseName, ex.Message), ex);
            }

            return Task.FromResult<ITriggerBinding>(new CosmosDBTriggerBinding(parameter, documentCollectionLocation, leaseCollectionLocation, leaseHostOptions));
        }

        private string ResolveAttributeConnectionString(CosmosDBTriggerAttribute attribute)
        {
            if (string.IsNullOrEmpty(_monitorConnectionString) &&
                string.IsNullOrEmpty(attribute.ConnectionStringSetting))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture,
                    "The DocumentDBTrigger connection string must be set either via a '{0}' app setting, via the DocumentDBTriggerAttribute.ConnectionString property or via DocumentDBConfiguration.TriggerConnectionString.",
                    DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName));
            }

            return attribute.ConnectionStringSetting ?? _monitorConnectionString;
        }

        private string ResolveAttributeLeasesConnectionString(CosmosDBTriggerAttribute attribute, string triggerConnectionString)
        {
            // If the leases connection is not specified, it connects to the monitored service
            return attribute.LeaseConnectionStringSetting ?? _leasesConnectionString ?? triggerConnectionString;
        }

        private ChangeFeedHostOptions ResolveLeaseOptions(CosmosDBTriggerAttribute attribute)
        {
            return attribute.LeaseOptions ?? _leasesOptions;
        }
    }
}
