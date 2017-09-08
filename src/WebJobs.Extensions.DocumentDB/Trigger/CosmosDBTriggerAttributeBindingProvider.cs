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
        private readonly ChangeFeedHostOptions _leasesOptions;
        private readonly INameResolver _nameResolver;
        private string _monitorConnectionString;
        private string _leasesConnectionString;
        private DocumentDBConfiguration _config;

        public CosmosDBTriggerAttributeBindingProvider(INameResolver nameResolver, DocumentDBConfiguration config, ChangeFeedHostOptions leasesOptions = null)
        {
            _nameResolver = nameResolver;
            _config = config;
            _leasesOptions = leasesOptions ?? new ChangeFeedHostOptions();
        }

        public async Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
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
                return null;
            }

            _monitorConnectionString = _nameResolver.Resolve(DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName);
            _leasesConnectionString = _nameResolver.Resolve(DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName);

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

                string leasesConnectionString = ResolveAttributeLeasesConnectionString(attribute, triggerConnectionString);
                DocumentDBConnectionString leasesConnection = new DocumentDBConnectionString(leasesConnectionString);
                if (leasesConnection.ServiceEndpoint == null)
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

                if (attribute.CreateLeaseCollectionIfNotExists)
                {
                    // Not disposing this because it might be reused on other Trigger since Triggers could share lease collection
                    IDocumentDBService service = _config.GetService(leasesConnectionString);
                    await DocumentDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(service, leaseCollectionLocation.DatabaseName, leaseCollectionLocation.CollectionName, null, attribute.LeasesCollectionThroughput);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(string.Format("Cannot create Collection Information for {0} in database {1} with lease {2} in database {3} : {4}", attribute.CollectionName, attribute.DatabaseName, attribute.LeaseCollectionName, attribute.LeaseDatabaseName, ex.Message), ex);
            }

            return new CosmosDBTriggerBinding(parameter, documentCollectionLocation, leaseCollectionLocation, leaseHostOptions);
        }

        private string ResolveAttributeConnectionString(CosmosDBTriggerAttribute attribute)
        {
            string connectionString = this._monitorConnectionString;
            if (!TryReadFromSettings(attribute.ConnectionStringSetting, connectionString, out connectionString))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture,
                    "The CosmosDBTriggerAttribute.ConnectionStringSetting '{0}' property specified does not exist in the Application Settings.",
                    attribute.ConnectionStringSetting));
            }

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture,
                    "The CosmosDBTrigger connection string must be set either via a '{0}' app setting, via the CosmosDBTriggerAttribute.ConnectionStringSetting property or via DocumentDBConfiguration.ConnectionString.",
                    DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName));
            }

            return connectionString;
        }

        private string ResolveAttributeLeasesConnectionString(CosmosDBTriggerAttribute attribute, string triggerConnectionString)
        {
            // If the leases connection is not specified, it connects to the monitored service
            string connectionString = string.IsNullOrEmpty(this._leasesConnectionString) ? triggerConnectionString : this._leasesConnectionString;
            if (!TryReadFromSettings(attribute.LeaseConnectionStringSetting, connectionString, out connectionString))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture,
                    "The CosmosDBTriggerAttribute.LeaseConnectionStringSetting '{0}' property specified does not exist in the Application Settings.",
                    attribute.ConnectionStringSetting));
            }

            return connectionString;
        }

        private ChangeFeedHostOptions ResolveLeaseOptions(CosmosDBTriggerAttribute attribute)
        {
            return attribute.LeaseOptions ?? _leasesOptions;
        }

        private bool TryReadFromSettings(string settingsKey, string defaultValue, out string settingsValue)
        {
            if (string.IsNullOrEmpty(settingsKey))
            {
                settingsValue = defaultValue;
                return true;
            }
            settingsValue = _nameResolver.Resolve(settingsKey);
            return !string.IsNullOrEmpty(settingsValue);
        }
    }
}