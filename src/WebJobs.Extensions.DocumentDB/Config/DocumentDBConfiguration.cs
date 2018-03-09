// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.ChangeFeedProcessor;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB.Bindings;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    /// <summary>
    /// Defines the configuration options for the DocumentDB binding.
    /// </summary>
    public class DocumentDBConfiguration : IExtensionConfigProvider
    {
        internal const string AzureWebJobsDocumentDBConnectionStringName = "AzureWebJobsDocumentDBConnectionString";
        internal readonly ConcurrentDictionary<string, IDocumentDBService> ClientCache = new ConcurrentDictionary<string, IDocumentDBService>();
        private string _defaultConnectionString;
        private TraceWriter _trace;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public DocumentDBConfiguration()
        {
            DocumentDBServiceFactory = new DefaultDocumentDBServiceFactory();
        }

        internal IDocumentDBServiceFactory DocumentDBServiceFactory { get; set; }

        /// <summary>
        /// Gets or sets the DocumentDB connection string.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the lease options for the DocumentDB Trigger. 
        /// </summary>
        public ChangeFeedHostOptions LeaseOptions { get; set; }

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            _trace = context.Trace;

            INameResolver nameResolver = context.Config.GetService<INameResolver>();

            IConverterManager converterManager = context.Config.GetService<IConverterManager>();

            // Use this if there is no other connection string set.
            _defaultConnectionString = nameResolver.Resolve(AzureWebJobsDocumentDBConnectionStringName);

            BindingFactory factory = new BindingFactory(nameResolver, converterManager);

            IBindingProvider outputProvider = factory.BindToCollector<DocumentDBAttribute, OpenType>(typeof(DocumentDBCollectorBuilder<>), this);

            IBindingProvider clientProvider = factory.BindToInput<DocumentDBAttribute, DocumentClient>(new DocumentDBClientBuilder(this));

            IBindingProvider jArrayProvider = factory.BindToInput<DocumentDBAttribute, JArray>(typeof(DocumentDBJArrayBuilder), this);

            IBindingProvider enumerableProvider = factory.BindToInput<DocumentDBAttribute, IEnumerable<OpenType>>(typeof(DocumentDBEnumerableBuilder<>), this);
            enumerableProvider = factory.AddValidator<DocumentDBAttribute>(ValidateInputBinding, enumerableProvider);

            IBindingProvider inputProvider = factory.BindToGenericValueProvider<DocumentDBAttribute>((attr, t) => BindForItemAsync(attr, t));
            inputProvider = factory.AddValidator<DocumentDBAttribute>(ValidateInputBinding, inputProvider);

            IExtensionRegistry extensions = context.Config.GetService<IExtensionRegistry>();
            extensions.RegisterBindingRules<DocumentDBAttribute>(ValidateConnection, nameResolver, outputProvider, clientProvider, jArrayProvider, enumerableProvider, inputProvider);

            context.Config.RegisterBindingExtensions(new CosmosDBTriggerAttributeBindingProvider(nameResolver, _trace, this, LeaseOptions));
        }

        internal static void ValidateInputBinding(DocumentDBAttribute attribute, Type parameterType)
        {
            bool hasSqlQuery = !string.IsNullOrEmpty(attribute.SqlQuery);
            bool hasId = !string.IsNullOrEmpty(attribute.Id);

            if (hasSqlQuery && hasId)
            {
                throw new InvalidOperationException($"Only one of 'SqlQuery' and '{nameof(DocumentDBAttribute.Id)}' can be specified.");
            }

            if (IsSupportedEnumerable(parameterType))
            {
                if (hasId)
                {
                    throw new InvalidOperationException($"'{nameof(DocumentDBAttribute.Id)}' cannot be specified when binding to an IEnumerable property.");
                }
            }
            else if (!hasId)
            {
                throw new InvalidOperationException($"'{nameof(DocumentDBAttribute.Id)}' is required when binding to a {parameterType.Name} property.");
            }
        }

        internal void ValidateConnection(DocumentDBAttribute attribute, Type paramType)
        {
            if (string.IsNullOrEmpty(ConnectionString) &&
                string.IsNullOrEmpty(attribute.ConnectionStringSetting) &&
                string.IsNullOrEmpty(_defaultConnectionString))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture,
                    "The DocumentDB connection string must be set either via a '{0}' app setting, via the DocumentDBAttribute.ConnectionStringSetting property or via DocumentDBConfiguration.ConnectionString.",
                    AzureWebJobsDocumentDBConnectionStringName));
            }
        }

        internal DocumentClient BindForClient(DocumentDBAttribute attribute)
        {
            string resolvedConnectionString = ResolveConnectionString(attribute.ConnectionStringSetting);
            IDocumentDBService service = GetService(resolvedConnectionString);

            return service.GetClient();
        }

        internal Task<IValueBinder> BindForItemAsync(DocumentDBAttribute attribute, Type type)
        {
            if (string.IsNullOrEmpty(attribute.Id))
            {
                throw new InvalidOperationException("The 'Id' property of a DocumentDB single-item input binding cannot be null or empty.");
            }

            DocumentDBContext context = CreateContext(attribute);

            Type genericType = typeof(DocumentDBItemValueBinder<>).MakeGenericType(type);
            IValueBinder binder = (IValueBinder)Activator.CreateInstance(genericType, context);

            return Task.FromResult(binder);
        }

        internal string ResolveConnectionString(string attributeConnectionString)
        {
            // First, try the Attribute's string.
            if (!string.IsNullOrEmpty(attributeConnectionString))
            {
                return attributeConnectionString;
            }

            // Second, try the config's ConnectionString
            if (!string.IsNullOrEmpty(ConnectionString))
            {
                return ConnectionString;
            }

            // Finally, fall back to the default.
            return _defaultConnectionString;
        }
        
        internal IDocumentDBService GetService(string connectionString)
        {
            return ClientCache.GetOrAdd(connectionString, (c) => DocumentDBServiceFactory.CreateService(c));
        }

        internal DocumentDBContext CreateContext(DocumentDBAttribute attribute)
        {
            string resolvedConnectionString = ResolveConnectionString(attribute.ConnectionStringSetting);

            IDocumentDBService service = GetService(resolvedConnectionString);

            return new DocumentDBContext
            {
                Service = service,
                Trace = _trace,
                ResolvedAttribute = attribute,
            };
        }

        internal static bool IsSupportedEnumerable(Type type)
        {
            if (type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return true;
            }

            return false;
        }
    }
}