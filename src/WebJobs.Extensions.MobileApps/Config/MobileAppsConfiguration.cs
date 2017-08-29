// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.MobileApps.Bindings;
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
        private INameResolver _nameResolver;

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

            _nameResolver = context.Config.GetService<INameResolver>();
            IConverterManager converterManager = context.Config.GetService<IConverterManager>();

            // Set defaults, to be used if no other values are found:
            _defaultApiKey = _nameResolver.Resolve(AzureWebJobsMobileAppApiKeyName);

            string uriString = _nameResolver.Resolve(AzureWebJobsMobileAppUriName);
            Uri.TryCreate(uriString, UriKind.Absolute, out _defaultMobileAppUri);

            BindingFactory factory = new BindingFactory(_nameResolver, converterManager);

            IBindingProvider outputProvider = factory.BindToCollector<MobileTableAttribute, OpenType>(typeof(MobileTableCollectorBuilder<>), this);

            IBindingProvider clientProvider = factory.BindToInput<MobileTableAttribute, IMobileServiceClient>(new MobileTableClientBuilder(this));

            IBindingProvider queryProvider = factory.BindToInput<MobileTableAttribute, IMobileServiceTableQuery<OpenType>>(typeof(MobileTableQueryBuilder<>), this);
            queryProvider = factory.AddFilter<MobileTableAttribute>(IsQueryType, queryProvider);

            IBindingProvider jObjectTableProvider = factory.BindToInput<MobileTableAttribute, IMobileServiceTable>(new MobileTableJObjectTableBuilder(this));

            IBindingProvider tableProvider = factory.BindToInput<MobileTableAttribute, IMobileServiceTable<OpenType>>(typeof(MobileTablePocoTableBuilder<>), this);
            tableProvider = factory.AddFilter<MobileTableAttribute>(IsTableType, tableProvider);

            IBindingProvider itemProvider = factory.BindToGenericValueProvider<MobileTableAttribute>(BindForItemAsync);
            itemProvider = factory.AddFilter<MobileTableAttribute>(IsItemType, itemProvider);

            IExtensionRegistry extensions = context.Config.GetService<IExtensionRegistry>();
            extensions.RegisterBindingRules<MobileTableAttribute>(ValidateMobileAppUri, _nameResolver, outputProvider, clientProvider, jObjectTableProvider, queryProvider, tableProvider, itemProvider);
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
            // The behavior for ApiKey is unique, so we do not use the AutoResolve
            // functionality.
            // If an attribute sets the ApiKeySetting to an empty string,
            // that overwrites any default value and sets it to null.
            // If ApiKeySetting is null, it returns the default value.

            // First, if the key is an empty string, return null.
            if (attributeApiKey != null && attributeApiKey.Length == 0)
            {
                return null;
            }

            // Second, if it is anything other than null, return the resolved value
            if (attributeApiKey != null)
            {
                return _nameResolver.Resolve(attributeApiKey);
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