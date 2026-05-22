// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    /// <summary>
    /// Detects the user defined type T on the binding and calls the <see cref="CosmosDBTriggerAttributeBindingProvider{T}"/>.
    /// </summary>
    internal class CosmosDBTriggerAttributeBindingProviderGenerator : ITriggerBindingProvider
    {
        private readonly INameResolver _nameResolver;
        private readonly CosmosDBOptions _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IDrainModeManager _drainModeManager;
        private readonly CosmosDBExtensionConfigProvider _configProvider;

        public CosmosDBTriggerAttributeBindingProviderGenerator(INameResolver nameResolver,
            CosmosDBOptions options,
            CosmosDBExtensionConfigProvider configProvider,
            IDrainModeManager drainModeManager,
            ILoggerFactory loggerFactory)
        {
            _nameResolver = nameResolver;
            _options = options;
            _configProvider = configProvider;
            _drainModeManager = drainModeManager;
            _loggerFactory = loggerFactory;
        }

        public static Type GetParameterType(ParameterInfo parameter) => parameter.ParameterType.GenericTypeArguments.Length > 0 ? parameter.ParameterType.GenericTypeArguments[0] : parameter.ParameterType;

        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            CosmosDBTriggerAttribute cosmosDBTriggerAttribute = context.Parameter.GetCustomAttribute<CosmosDBTriggerAttribute>(inherit: false);

            if (cosmosDBTriggerAttribute == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            Type documentType = CosmosDBTriggerAttributeBindingProviderGenerator.GetParameterType(context.Parameter);

            if (typeof(JArray).IsAssignableFrom(documentType)
                || typeof(JObject[]).IsAssignableFrom(documentType))
            {
                documentType = typeof(JObject); // When binding to JArray, use JObject as contract.
            }

            if (typeof(string).IsAssignableFrom(documentType))
            {
                documentType = typeof(JObject);
            }

            // For out-of-process (worker) functions in AllVersionsAndDeletes mode, the parameter
            // type surfaced to the host is the raw payload (JObject - after the JArray/string
            // collapse above) rather than the worker's actual ChangeFeedItem<T> binding. Wrap so
            // downstream type contracts (TriggerValueType, value binder, listener) all agree on
            // ChangeFeedItem<JObject>. Intentionally NOT shimming arbitrary user POCOs: those are
            // in-proc bindings where the user is expected to declare ChangeFeedItem<T> directly,
            // and the listener's type guard will produce a clear InvalidOperationException at
            // startup if they don't.
            if (cosmosDBTriggerAttribute.ChangeFeedMode == CosmosDBChangeFeedMode.AllVersionsAndDeletes
                && documentType == typeof(JObject))
            {
                documentType = typeof(ChangeFeedItem<JObject>);
            }

            Type baseType = typeof(CosmosDBTriggerAttributeBindingProvider<>);

            Type genericBindingType = baseType.MakeGenericType(documentType);

            Type[] typeArgs = { typeof(INameResolver), typeof(CosmosDBOptions), typeof(CosmosDBExtensionConfigProvider), typeof(IDrainModeManager), typeof(ILoggerFactory) };

            ConstructorInfo constructor = genericBindingType.GetConstructor(typeArgs);

            object[] constructorParameterValues = { _nameResolver, _options, _configProvider, _drainModeManager, _loggerFactory };

            object cosmosDBTriggerAttributeBindingProvider = constructor.Invoke(constructorParameterValues);

            MethodInfo methodInfo = genericBindingType.GetMethod(nameof(CosmosDBTriggerAttributeBindingProvider<dynamic>.TryCreateAsync));

            object[] methodParameterValues = { context };

            return (Task<ITriggerBinding>)methodInfo.Invoke(cosmosDBTriggerAttributeBindingProvider, methodParameterValues);
        }
    }
}
