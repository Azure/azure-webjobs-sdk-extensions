// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.WindowsAzure.MobileServices;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps
{
    /// <summary>
    /// Defines the configuration options for the motile table binding.
    /// </summary>
    public class MobileAppsConfiguration : IExtensionConfigProvider
    {
        internal const string AzureWebJobsMobileAppUriName = "AzureWebJobsMobileAppUri";
        internal const string AzureWebJobsMobileAppApiKeyName = "AzureWebJobsMobileAppApiKey";
        internal readonly ConcurrentDictionary<string, IMobileServiceClient> ClientCache = new ConcurrentDictionary<string, IMobileServiceClient>();

        private string _defaultApiKey;
        private Uri _defaultMobileAppUri;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public MobileAppsConfiguration()
        {
            this.ClientFactory = new DefaultMobileServiceClientFactory();
        }

        /// <summary>
        /// Gets or sets the ApiKey to use with the Mobile App.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the mobile app URI.
        /// </summary>      
        public Uri MobileAppUri { get; set; }

        internal IMobileServiceClientFactory ClientFactory { get; set; }

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            INameResolver nameResolver = context.Config.GetService<INameResolver>();
            IConverterManager converterManager = context.Config.GetService<IConverterManager>();

            // Set defaults, to be used if no other values are found:
            _defaultApiKey = nameResolver.Resolve(AzureWebJobsMobileAppApiKeyName);

            string uriString = nameResolver.Resolve(AzureWebJobsMobileAppUriName);
            Uri.TryCreate(uriString, UriKind.Absolute, out _defaultMobileAppUri);

            BindingFactory factory = new BindingFactory(nameResolver, converterManager);

            IBindingProvider outputProvider = factory.BindToGenericAsyncCollector<MobileTableAttribute>(BindForOutput, ThrowIfInvalidOutputItemType);

            IBindingProvider clientProvider = factory.BindToExactType<MobileTableAttribute, IMobileServiceClient>(BindForClient);

            IBindingProvider queryProvider = factory.BindToGenericItem<MobileTableAttribute>(BindForQueryAsync);
            queryProvider = factory.AddFilter<MobileTableAttribute>(IsQueryType, queryProvider);

            IBindingProvider jObjectTableProvider = factory.BindToExactType<MobileTableAttribute, IMobileServiceTable>(BindForTable);

            IBindingProvider tableProvider = factory.BindToGenericItem<MobileTableAttribute>(BindForTableAsync);
            tableProvider = factory.AddFilter<MobileTableAttribute>(IsTableType, tableProvider);

            IBindingProvider itemProvider = factory.BindToGenericValueProvider<MobileTableAttribute>(BindForItemAsync);
            itemProvider = factory.AddFilter<MobileTableAttribute>(IsItemType, itemProvider);

            IExtensionRegistry extensions = context.Config.GetService<IExtensionRegistry>();
            extensions.RegisterBindingRules<MobileTableAttribute>(ValidateMobileAppUri, nameResolver, outputProvider, clientProvider, jObjectTableProvider, queryProvider, tableProvider, itemProvider);
        }

        internal static bool IsQueryType(MobileTableAttribute attribute, Type paramType)
        {
            if (paramType.IsGenericType &&
                paramType.GetGenericTypeDefinition() == typeof(IMobileServiceTableQuery<>))
            {
                Type tableType = paramType.GetGenericArguments().Single();
                ThrowIfInvalidItemType(attribute, tableType);

                return true;
            }

            return false;
        }

        internal bool IsTableType(MobileTableAttribute attribute, Type paramType)
        {
            // We will check if the argument is valid in a Validator
            if (paramType.IsGenericType &&
                paramType.GetGenericTypeDefinition() == typeof(IMobileServiceTable<>))
            {
                Type tableType = paramType.GetGenericArguments().Single();
                ThrowIfInvalidItemType(attribute, tableType);

                return true;
            }

            return false;
        }

        internal bool IsItemType(MobileTableAttribute attribute, Type paramType)
        {
            ThrowIfInvalidItemType(attribute, paramType);

            if (string.IsNullOrEmpty(attribute.Id))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "'Id' must be set when using a parameter of type '{0}'.", paramType.Name));
            }

            return true;
        }

        internal static void ThrowIfGenericArgumentIsInvalid(MobileTableAttribute attribute, Type paramType)
        {
            // Assume IsQueryType or IsTableType has already run -- so we know there is only one argument
            Type argumentType = paramType.GetGenericArguments().Single();
            ThrowIfInvalidItemType(attribute, argumentType);
        }

        internal static bool ThrowIfInvalidOutputItemType(MobileTableAttribute attribute, Type paramType)
        {
            // We explicitly allow object as a type to enable anonymous types, but TableName must be specified.
            if (paramType == typeof(object))
            {
                if (string.IsNullOrEmpty(attribute.TableName))
                {
                    throw new InvalidOperationException("A parameter of type 'object' must have table name specified.");
                }

                return true;
            }

            return ThrowIfInvalidItemType(attribute, paramType);
        }

        internal static bool ThrowIfInvalidItemType(MobileTableAttribute attribute, Type paramType)
        {
            if (!MobileAppUtility.IsValidItemType(paramType, attribute.TableName))
            {
                throw new ArgumentException(string.Format("The type '{0}' cannot be used in a MobileTable binding. The type must either be 'JObject' or have a public string 'Id' property.", paramType.Name));
            }

            return true;
        }

        internal void ValidateMobileAppUri(MobileTableAttribute attribute, Type paramType)
        {
            if (MobileAppUri == null &&
                string.IsNullOrEmpty(attribute.MobileAppUriSetting) &&
                _defaultMobileAppUri == null)
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture,
                    "The mobile app Uri must be set either via a '{0}' app setting, via the MobileTableAttribute.MobileAppUriSetting property or via MobileAppsConfiguration.MobileAppUri.",
                    AzureWebJobsMobileAppUriName));
            }
        }

        internal Task<IValueBinder> BindForItemAsync(MobileTableAttribute attribute, Type paramType)
        {
            MobileTableContext context = CreateContext(attribute);

            Type genericType = typeof(MobileTableItemValueBinder<>).MakeGenericType(paramType);
            IValueBinder binder = (IValueBinder)Activator.CreateInstance(genericType, context);

            return Task.FromResult(binder);
        }

        internal IMobileServiceTable BindForTable(MobileTableAttribute attribute)
        {
            MobileTableContext context = CreateContext(attribute);
            return context.Client.GetTable(context.ResolvedAttribute.TableName);
        }

        internal async Task<object> BindForQueryAsync(MobileTableAttribute attribute, Type paramType)
        {
            object table = await BindForTableAsync(attribute, paramType);
            MethodInfo createQueryMethod = table.GetType().GetMethod("CreateQuery");

            return createQueryMethod.Invoke(table, null);
        }

        internal Task<object> BindForTableAsync(MobileTableAttribute attribute, Type paramType)
        {
            MobileTableContext context = CreateContext(attribute);

            // Assume that the Filter has already run.
            Type tableType = paramType.GetGenericArguments().Single();

            // If TableName is specified, add it to the internal table cache. Now items of this type
            // will operate on the specified TableName.
            if (!string.IsNullOrEmpty(context.ResolvedAttribute.TableName))
            {
                context.Client.AddToTableNameCache(tableType, context.ResolvedAttribute.TableName);
            }

            MethodInfo getTableMethod = GetGenericTableMethod();
            MethodInfo getTableGenericMethod = getTableMethod.MakeGenericMethod(tableType);

            return Task.FromResult(getTableGenericMethod.Invoke(context.Client, null));
        }

        private static MethodInfo GetGenericTableMethod()
        {
            return typeof(IMobileServiceClient).GetMethods()
                .Where(m => m.IsGenericMethod && m.Name == "GetTable").Single();
        }

        internal IMobileServiceClient BindForClient(MobileTableAttribute attribute)
        {
            MobileTableContext context = CreateContext(attribute);
            return context.Client;
        }

        internal object BindForOutput(MobileTableAttribute attribute, Type paramType)
        {
            MobileTableContext context = CreateContext(attribute);

            Type collectorType = typeof(MobileTableAsyncCollector<>).MakeGenericType(paramType);

            return Activator.CreateInstance(collectorType, context);
        }

        internal Uri ResolveMobileAppUri(string attributeUriString)
        {
            // First, try the Attribute's Uri.
            Uri attributeUri;
            if (Uri.TryCreate(attributeUriString, UriKind.Absolute, out attributeUri))
            {
                return attributeUri;
            }

            // Second, try the config's Uri
            if (MobileAppUri != null)
            {
                return MobileAppUri;
            }

            // Finally, fall back to the default.
            return _defaultMobileAppUri;
        }

        internal MobileTableContext CreateContext(MobileTableAttribute attribute)
        {
            Uri resolvedUri = ResolveMobileAppUri(attribute.MobileAppUriSetting);
            string resolvedApiKey = ResolveApiKey(attribute.ApiKeySetting);

            return new MobileTableContext
            {
                Client = GetClient(resolvedUri, resolvedApiKey),
                ResolvedAttribute = attribute
            };
        }

        internal string ResolveApiKey(string attributeApiKey)
        {
            // If an attribute sets the ApiKeySetting to an empty string,
            // that overwrites any default value and sets it to null.
            // If ApiKeySetting is null, it returns the default value.

            // First, if the key is an empty string, return null.
            if (attributeApiKey != null && attributeApiKey.Length == 0)
            {
                return null;
            }

            // Second, if it is anything other than null, return the value
            if (attributeApiKey != null)
            {
                return attributeApiKey;
            }

            // Third, try the config's key
            if (!string.IsNullOrEmpty(ApiKey))
            {
                return ApiKey;
            }

            // Finally, fall back to the default.
            return _defaultApiKey;
        }

        internal IMobileServiceClient GetClient(Uri mobileAppUri, string apiKey)
        {
            string key = GetCacheKey(mobileAppUri, apiKey);
            return ClientCache.GetOrAdd(key, (c) => CreateMobileServiceClient(ClientFactory, mobileAppUri, apiKey));
        }

        internal static string GetCacheKey(Uri mobileAppUri, string apiKey)
        {
            return string.Format("{0};{1}", mobileAppUri, apiKey);
        }

        internal static IMobileServiceClient CreateMobileServiceClient(IMobileServiceClientFactory factory, Uri mobileAppUri, string apiKey = null)
        {
            HttpMessageHandler[] handlers = null;
            if (!string.IsNullOrEmpty(apiKey))
            {
                handlers = new[] { new MobileServiceApiKeyHandler(apiKey) };
            }

            return factory.CreateClient(mobileAppUri, handlers);
        }
    }
}