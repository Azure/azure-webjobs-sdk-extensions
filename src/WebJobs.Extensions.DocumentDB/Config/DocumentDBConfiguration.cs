// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;

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

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            INameResolver nameResolver = context.Config.GetService<INameResolver>();
            IConverterManager converterManager = context.Config.GetService<IConverterManager>();

            // Use this if there is no other connection string set.
            _defaultConnectionString = nameResolver.Resolve(AzureWebJobsDocumentDBConnectionStringName);

            BindingFactory factory = new BindingFactory(nameResolver, converterManager);

            IBindingProvider outputProvider = factory.BindToGenericAsyncCollector<DocumentDBAttribute>((attr, t) => BindForOutput(attr, t, context.Trace));

            IBindingProvider clientProvider = factory.BindToExactType<DocumentDBAttribute, DocumentClient>(BindForClient);

            IBindingProvider itemProvider = factory.BindToGenericValueProvider<DocumentDBAttribute>((attr, t) => BindForItemAsync(attr, t, context.Trace));

            IExtensionRegistry extensions = context.Config.GetService<IExtensionRegistry>();
            extensions.RegisterBindingRules<DocumentDBAttribute>(ValidateConnection, nameResolver, outputProvider, clientProvider, itemProvider);
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

        internal object BindForOutput(DocumentDBAttribute attribute, Type parameterType, TraceWriter trace)
        {
            DocumentDBContext context = CreateContext(attribute, trace);

            Type collectorType = typeof(DocumentDBAsyncCollector<>).MakeGenericType(parameterType);

            return Activator.CreateInstance(collectorType, context);
        }

        internal DocumentClient BindForClient(DocumentDBAttribute attribute)
        {
            string resolvedConnectionString = ResolveConnectionString(attribute.ConnectionStringSetting);
            IDocumentDBService service = GetService(resolvedConnectionString);

            return service.GetClient();
        }

        internal Task<IValueBinder> BindForItemAsync(DocumentDBAttribute attribute, Type type, TraceWriter trace)
        {
            DocumentDBContext context = CreateContext(attribute, trace);

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

        internal DocumentDBContext CreateContext(DocumentDBAttribute attribute, TraceWriter trace)
        {
            string resolvedConnectionString = ResolveConnectionString(attribute.ConnectionStringSetting);

            IDocumentDBService service = GetService(resolvedConnectionString);

            return new DocumentDBContext
            {
                Service = service,
                Trace = trace,
                ResolvedAttribute = attribute
            };
        }
    }
}