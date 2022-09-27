// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB.Bindings;
using Microsoft.Azure.WebJobs.Script.Abstractions.Description.Binding;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    /// <summary>
    /// Defines the configuration options for the CosmosDB binding.
    /// </summary>
    [Extension("CosmosDB")]
    internal class CosmosDBExtensionConfigProvider : IExtensionConfigProvider
    {
        private readonly ICosmosDBServiceFactory _cosmosDBServiceFactory;
        private readonly ICosmosDBSerializerFactory _cosmosSerializerFactory;
        private readonly INameResolver _nameResolver;
        private readonly CosmosDBOptions _options;
        private readonly ILoggerFactory _loggerFactory;

        public CosmosDBExtensionConfigProvider(
            IOptions<CosmosDBOptions> options,
            ICosmosDBServiceFactory cosmosDBServiceFactory,
            ICosmosDBSerializerFactory cosmosSerializerFactory,
            INameResolver nameResolver,
            ILoggerFactory loggerFactory)
        {
            _cosmosDBServiceFactory = cosmosDBServiceFactory;
            _cosmosSerializerFactory = cosmosSerializerFactory;
            _nameResolver = nameResolver;
            _options = options.Value;
            _loggerFactory = loggerFactory;
        }

        internal ConcurrentDictionary<string, CosmosClient> ClientCache { get; } = new ConcurrentDictionary<string, CosmosClient>();

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            // Apply ValidateConnection to all on this rule.
            var rule = context.AddBindingRule<CosmosDBAttribute>();
            rule.AddValidator(ValidateConnection);
            rule.BindToCollector<DocumentOpenType>(typeof(CosmosDBCollectorBuilder<>), this);

            rule.BindToInput<CosmosClient>(new CosmosDBClientBuilder(this));
            rule.BindToInput<ParameterBindingData>((attr) => CreateBindingData(attr));

            // Enumerable inputs
            rule.WhenIsNull(nameof(CosmosDBAttribute.Id))
                .BindToInput<JArray>(typeof(CosmosDBJArrayBuilder), this);

            rule.WhenIsNull(nameof(CosmosDBAttribute.Id))
                .BindToInput<IEnumerable<DocumentOpenType>>(typeof(CosmosDBEnumerableBuilder<>), this);

            // Single input
            rule.WhenIsNotNull(nameof(CosmosDBAttribute.Id))
                .WhenIsNull(nameof(CosmosDBAttribute.SqlQuery))
                .BindToValueProvider<DocumentOpenType>((attr, t) => BindForItemAsync(attr, t));

            // Trigger
            var rule2 = context.AddBindingRule<CosmosDBTriggerAttribute>();
            rule2.BindToTrigger(new CosmosDBTriggerAttributeBindingProviderGenerator(_nameResolver, _options, this, _loggerFactory));
            rule2.AddConverter<CosmosDBTriggerAttribute, ParameterBindingData>(ConvertToBindingData);
        }

        internal void ValidateConnection(CosmosDBAttribute attribute, Type paramType)
        {
            if (attribute.Connection == string.Empty)
            {
                string attributeProperty = $"{nameof(CosmosDBAttribute)}.{nameof(CosmosDBAttribute.Connection)}";
                throw new InvalidOperationException(
                    $"The {attributeProperty} property cannot be an empty value.");
            }
        }

        internal ParameterBindingData CreateBindingData(CosmosDBAttribute cosmosAttribute)
        {
            var connectionName = cosmosAttribute.Connection ?? Constants.DefaultConnectionStringName;
            var attributeProperties = cosmosAttribute.GetType().GetProperties();

            return CreateParameterBindingData(cosmosAttribute, attributeProperties, connectionName);
        }

        internal ParameterBindingData ConvertToBindingData(CosmosDBTriggerAttribute cosmosAttribute)
        {
            var connectionName = cosmosAttribute.Connection ?? Constants.DefaultConnectionStringName;
            var attributeProperties = cosmosAttribute.GetType().GetProperties();

            return CreateParameterBindingData(cosmosAttribute, attributeProperties, connectionName);
        }

        private ParameterBindingData CreateParameterBindingData(Attribute cosmosAttribute, PropertyInfo[] properties, string connectionName)
        {
            var bindingData = new ParameterBindingData();

            foreach (var attribute in properties)
            {
                bindingData.Properties.Add(attribute.Name, attribute.GetValue(cosmosAttribute)?.ToString());
            }

            if (bindingData.Properties.ContainsKey("Connection"))
            {
                bindingData.Properties["Connection"] = connectionName;
            }
            else
            {
                bindingData.Properties.Add("Connection", connectionName);
            }

            return bindingData;
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

        internal CosmosClient GetService(string connection, string preferredLocations = "", string userAgent = "")
        {
            string cacheKey = BuildCacheKey(connection, preferredLocations);
            if (!string.IsNullOrEmpty(_options.UserAgentSuffix))
            {
                userAgent += _options.UserAgentSuffix;
            }

            CosmosClientOptions cosmosClientOptions = CosmosDBUtility.BuildClientOptions(_options.ConnectionMode, _cosmosSerializerFactory.CreateSerializer(), preferredLocations, userAgent);
            return ClientCache.GetOrAdd(cacheKey, (c) => _cosmosDBServiceFactory.CreateService(connection, cosmosClientOptions));
        }

        internal CosmosDBContext CreateContext(CosmosDBAttribute attribute)
        {
            CosmosClient service = GetService(
                connection: attribute.Connection ?? Constants.DefaultConnectionStringName,
                preferredLocations: attribute.PreferredLocations);

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

        internal static string BuildCacheKey(string connectionString, string region) => $"{connectionString}|{region}";

        private class DocumentOpenType : OpenType.Poco
        {
            public override bool IsMatch(Type type, OpenTypeMatchContext context)
            {
                if (type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    return false;
                }

                if (type.FullName == "System.Object")
                {
                    return true;
                }

                return base.IsMatch(type, context);
            }
        }
    }
}