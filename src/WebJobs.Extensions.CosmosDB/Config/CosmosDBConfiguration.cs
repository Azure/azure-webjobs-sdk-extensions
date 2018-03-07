// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.ChangeFeedProcessor;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    /// <summary>
    /// Defines the configuration options for the CosmosDB binding.
    /// </summary>
    public class CosmosDBConfiguration : IExtensionConfigProvider
    {
        internal const string AzureWebJobsCosmosDBConnectionStringName = "AzureWebJobsCosmosDBConnectionString";
        internal readonly ConcurrentDictionary<string, ICosmosDBService> ClientCache = new ConcurrentDictionary<string, ICosmosDBService>();
        private string _defaultConnectionString;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public CosmosDBConfiguration()
        {
            CosmosDBServiceFactory = new DefaultCosmosDBServiceFactory();
        }

        internal ICosmosDBServiceFactory CosmosDBServiceFactory { get; set; }

        /// <summary>
        /// Gets or sets the CosmosDB connection string.
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

            INameResolver nameResolver = context.Config.GetService<INameResolver>();

            // Use this if there is no other connection string set.
            _defaultConnectionString = nameResolver.Resolve(AzureWebJobsCosmosDBConnectionStringName);

            // Apply ValidateConnection to all on this rule. 
            var rule = context.AddBindingRule<CosmosDBAttribute>();
            rule.AddValidator(ValidateConnection);
            rule.BindToCollector<OpenType>(typeof(CosmosDBCollectorBuilder<>), this);
            rule.BindToCollector<JObject>(typeof(CosmosDBCollectorBuilder<>), this);

            rule.BindToInput<DocumentClient>(new CosmosDBClientBuilder(this));
            rule.BindToInput<JArray>(typeof(CosmosDBJArrayBuilder), this);

            rule.BindToInput<IEnumerable<OpenType>>(typeof(CosmosDBEnumerableBuilder<>), this).AddValidator(ValidateInputBinding);

            rule.BindToValueProvider((attr, t) => BindForItemAsync(attr, t)).AddValidator(ValidateInputBinding);

            // Trigger
            context.Config.RegisterBindingExtensions(new CosmosDBTriggerAttributeBindingProvider(nameResolver, this, LeaseOptions));

            var rule2 = context.AddBindingRule<CosmosDBTriggerAttribute>();
            rule2.BindToTrigger<IReadOnlyList<Document>>();
            rule2.AddConverter<string, IReadOnlyList<Document>>(str => JsonConvert.DeserializeObject<IReadOnlyList<Document>>(str));
            rule2.AddConverter<IReadOnlyList<Document>, JArray>(docList => JArray.FromObject(docList));
            rule2.AddConverter<IReadOnlyList<Document>, string>(docList => JArray.FromObject(docList).ToString());
        }

        internal static void ValidateInputBinding(CosmosDBAttribute attribute, Type parameterType)
        {
            bool hasSqlQuery = !string.IsNullOrEmpty(attribute.SqlQuery);
            bool hasId = !string.IsNullOrEmpty(attribute.Id);

            if (hasSqlQuery && hasId)
            {
                throw new InvalidOperationException($"Only one of 'SqlQuery' and '{nameof(CosmosDBAttribute.Id)}' can be specified.");
            }

            if (IsSupportedEnumerable(parameterType))
            {
                if (hasId)
                {
                    throw new InvalidOperationException($"'{nameof(CosmosDBAttribute.Id)}' cannot be specified when binding to an IEnumerable property.");
                }
            }
            else if (!hasId)
            {
                throw new InvalidOperationException($"'{nameof(CosmosDBAttribute.Id)}' is required when binding to a {parameterType.Name} property.");
            }
        }

        internal void ValidateConnection(CosmosDBAttribute attribute, Type paramType)
        {
            if (string.IsNullOrEmpty(ConnectionString) &&
                string.IsNullOrEmpty(attribute.ConnectionStringSetting) &&
                string.IsNullOrEmpty(_defaultConnectionString))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture,
                    "The CosmosDB connection string must be set either via a '{0}' app setting, via the CosmosDBAttribute.ConnectionStringSetting property or via CosmosDBConfiguration.ConnectionString.",
                    AzureWebJobsCosmosDBConnectionStringName));
            }
        }

        internal DocumentClient BindForClient(CosmosDBAttribute attribute)
        {
            string resolvedConnectionString = ResolveConnectionString(attribute.ConnectionStringSetting);
            ICosmosDBService service = GetService(resolvedConnectionString);

            return service.GetClient();
        }

        internal Task<IValueBinder> BindForItemAsync(CosmosDBAttribute attribute, Type type)
        {
            if (string.IsNullOrEmpty(attribute.Id))
            {
                throw new InvalidOperationException("The 'Id' property of a CosmosDB single-item input binding cannot be null or empty.");
            }

            CosmosDBContext context = CreateContext(attribute);

            Type genericType = typeof(CosmosDBItemValueBinder<>).MakeGenericType(type);
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

        internal ICosmosDBService GetService(string connectionString)
        {
            return ClientCache.GetOrAdd(connectionString, (c) => CosmosDBServiceFactory.CreateService(c));
        }

        internal CosmosDBContext CreateContext(CosmosDBAttribute attribute)
        {
            string resolvedConnectionString = ResolveConnectionString(attribute.ConnectionStringSetting);

            ICosmosDBService service = GetService(resolvedConnectionString);

            return new CosmosDBContext
            {
                Service = service,
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