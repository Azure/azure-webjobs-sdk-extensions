// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.WindowsAzure.MobileServices;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps
{
    internal class MobileTableAttributeBindingProvider : IBindingProvider
    {
        private MobileAppsConfiguration _mobileAppsConfig;
        private JobHostConfiguration _jobHostConfig;
        private INameResolver _nameResolver;

        public MobileTableAttributeBindingProvider(JobHostConfiguration config, MobileAppsConfiguration mobileAppsConfig, INameResolver nameResolver)
        {
            _jobHostConfig = config;
            _mobileAppsConfig = mobileAppsConfig;
            _nameResolver = nameResolver;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            MobileTableAttribute attribute = parameter.GetMobileTableAttribute();
            if (attribute == null)
            {
                return Task.FromResult<IBinding>(null);
            }

            if (_mobileAppsConfig.MobileAppUri == null &&
                string.IsNullOrEmpty(attribute.MobileAppUri))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture,
                    "The mobile app Uri must be set either via a '{0}' app setting, via the MobileTableAttribute.MobileAppUri property or via MobileAppsConfiguration.MobileAppUri.",
                    MobileAppsConfiguration.AzureWebJobsMobileAppUriName));
            }

            MobileTableContext mobileTableContext = CreateContext(_mobileAppsConfig, attribute, _nameResolver);

            IBindingProvider compositeProvider = new CompositeBindingProvider(new IBindingProvider[]
            {
                new MobileTableOutputBindingProvider(_jobHostConfig, mobileTableContext),
                new MobileTableQueryBinding(context.Parameter, mobileTableContext),
                new MobileTableTableBinding(parameter, mobileTableContext),
                new MobileTableItemBinding(parameter, mobileTableContext, context)
            });

            return compositeProvider.TryCreateAsync(context);
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

        internal static MobileTableContext CreateContext(MobileAppsConfiguration config, MobileTableAttribute attribute, INameResolver resolver)
        {
            Uri resolvedMobileAppUri = config.MobileAppUri;
            string resolvedApiKey = config.ApiKey;

            // Override the config Uri with value from the attribute, if present.
            if (!string.IsNullOrEmpty(attribute.MobileAppUri))
            {
                string uriString = MobileAppsConfiguration.GetSettingFromConfigOrEnvironment(attribute.MobileAppUri);
                resolvedMobileAppUri = new Uri(uriString);
            }

            // If the attribute specifies an empty string ApiKey, set the ApiKey to null.
            if (attribute.ApiKey == string.Empty)
            {
                resolvedApiKey = null;
            }
            else if (attribute.ApiKey != null)
            {
                resolvedApiKey = MobileAppsConfiguration.GetSettingFromConfigOrEnvironment(attribute.ApiKey);
            }

            return new MobileTableContext
            {
                Config = config,
                Client = CreateMobileServiceClient(config.ClientFactory, resolvedMobileAppUri, resolvedApiKey),
                ResolvedId = Resolve(attribute.Id, resolver),
                ResolvedTableName = Resolve(attribute.TableName, resolver)
            };
        }

        private static string Resolve(string value, INameResolver resolver)
        {
            if (resolver == null)
            {
                return value;
            }

            return resolver.ResolveWholeString(value);
        }
    }
}