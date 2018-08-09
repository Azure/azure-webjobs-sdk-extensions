// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.MobileApps.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.MobileServices;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps
{
    /// <summary>
    /// Defines the configuration options for the motile table binding.
    /// </summary>
    [Extension("MobileApps")]
    internal class MobileAppsExtensionConfigProvider : IExtensionConfigProvider
    {
        internal const string AzureWebJobsMobileAppUriName = "AzureWebJobsMobileAppUri";
        internal const string AzureWebJobsMobileAppApiKeyName = "AzureWebJobsMobileAppApiKey";
        internal readonly ConcurrentDictionary<string, IMobileServiceClient> ClientCache = new ConcurrentDictionary<string, IMobileServiceClient>();

        private readonly INameResolver _nameResolver;
        private readonly IOptions<MobileAppsOptions> _options;
        private string _defaultApiKey;
        private Uri _defaultMobileAppUri;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public MobileAppsExtensionConfigProvider(IOptions<MobileAppsOptions> options, IMobileServiceClientFactory clientFactory, INameResolver nameResolver)
        {
            _options = options;
            _nameResolver = nameResolver;
            this.ClientFactory = clientFactory;
        }

        internal IMobileServiceClientFactory ClientFactory { get; set; }

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            // Set defaults, to be used if no other values are found:
            _defaultApiKey = _nameResolver.Resolve(AzureWebJobsMobileAppApiKeyName);

            string uriString = _nameResolver.Resolve(AzureWebJobsMobileAppUriName);
            Uri.TryCreate(uriString, UriKind.Absolute, out _defaultMobileAppUri);
            
            var rule = context.AddBindingRule<MobileTableAttribute>();
            rule.AddValidator(ValidateMobileAppUri);

            rule.BindToCollector<OpenType>(typeof(MobileTableCollectorBuilder<>), this);
            rule.BindToInput<IMobileServiceClient>(new MobileTableClientBuilder(this));

            // MobileType matching needs to know whether the attribute defines 'TableName', but 
            // OpenTypes can't get access to the attribute. So use filters to split into 2 cases. 
            rule.WhenIsNotNull(nameof(MobileTableAttribute.TableName)).
                BindToInput<IMobileServiceTableQuery<MobileTypeWithTableName>>(typeof(MobileTableQueryBuilder<>), this);
            rule.WhenIsNull(nameof(MobileTableAttribute.TableName)).
                BindToInput<IMobileServiceTableQuery<MobileTypeWithoutTableName>>(typeof(MobileTableQueryBuilder<>), this);

            rule.BindToInput<IMobileServiceTable>(new MobileTableJObjectTableBuilder(this));

            rule.WhenIsNotNull(nameof(MobileTableAttribute.TableName)).
                BindToInput<IMobileServiceTable<MobileTypeWithTableName>>(typeof(MobileTablePocoTableBuilder<>), this);
            rule.WhenIsNull(nameof(MobileTableAttribute.TableName)).
                BindToInput<IMobileServiceTable<MobileTypeWithoutTableName>>(typeof(MobileTablePocoTableBuilder<>), this);
                      
            rule.WhenIsNotNull(nameof(MobileTableAttribute.TableName)).
                BindToValueProvider<MobileTypeWithTableName>(BindForItemAsync).AddValidator(HasId);
            rule.WhenIsNull(nameof(MobileTableAttribute.TableName)).
                BindToValueProvider<MobileTypeWithoutTableName>(BindForItemAsync).AddValidator(HasId);
        }

        private void HasId(MobileTableAttribute attribute, Type paramType)
        {
            if (string.IsNullOrEmpty(attribute.Id))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "'Id' must be set when using a parameter of type '{0}'.", paramType.Name));
            }
        }

        internal static bool ThrowIfInvalidItemType(bool hasTableName, Type paramType)
        {
            if (!MobileAppUtility.IsValidItemType(paramType, hasTableName ? "true" : null))
            {
                throw new ArgumentException(string.Format("The type '{0}' cannot be used in a MobileTable binding. The type must either be 'JObject' or have a public string 'Id' property.", paramType.Name));
            }

            return true;
        }

        internal void ValidateMobileAppUri(MobileTableAttribute attribute, Type paramType)
        {
            if (_options.Value.MobileAppUri == null &&
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
            if (_options.Value.MobileAppUri != null)
            {
                return _options.Value.MobileAppUri;
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
            if (!string.IsNullOrEmpty(_options.Value.ApiKey))
            {
                return _options.Value.ApiKey;
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

        internal class MobileTypeWithoutTableName : OpenType
        {
            public override bool IsMatch(Type type, OpenTypeMatchContext context)
            {
                return ThrowIfInvalidItemType(false, type);
            }
        }

        internal class MobileTypeWithTableName : OpenType
        {
            public override bool IsMatch(Type type, OpenTypeMatchContext context)
            {
                return ThrowIfInvalidItemType(true, type);
            }
        }
    }
}