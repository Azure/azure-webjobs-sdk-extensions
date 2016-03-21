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

namespace Microsoft.Azure.WebJobs.Extensions.EasyTables
{
    internal class EasyTableAttributeBindingProvider : IBindingProvider
    {
        private EasyTablesConfiguration _easyTableConfig;
        private JobHostConfiguration _jobHostConfig;
        private INameResolver _nameResolver;

        public EasyTableAttributeBindingProvider(JobHostConfiguration config, EasyTablesConfiguration easyTableConfig, INameResolver nameResolver)
        {
            _jobHostConfig = config;
            _easyTableConfig = easyTableConfig;
            _nameResolver = nameResolver;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            EasyTableAttribute attribute = parameter.GetEasyTableAttribute();
            if (attribute == null)
            {
                return Task.FromResult<IBinding>(null);
            }

            if (_easyTableConfig.MobileAppUri == null &&
                string.IsNullOrEmpty(attribute.MobileAppUri))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture,
                    "The Easy Tables Uri must be set either via a '{0}' app setting, via the EasyTableAttribute.MobileAppUri property or via EasyTableConfiguration.MobileAppUri.",
                    EasyTablesConfiguration.AzureWebJobsMobileAppUriName));
            }

            EasyTableContext easyTableContext = CreateContext(_easyTableConfig, attribute, _nameResolver);

            IBindingProvider compositeProvider = new CompositeBindingProvider(new IBindingProvider[]
            {
                new EasyTableOutputBindingProvider(_jobHostConfig, easyTableContext),
                new EasyTableQueryBinding(context.Parameter, easyTableContext),
                new EasyTableTableBinding(parameter, easyTableContext),
                new EasyTableItemBinding(parameter, easyTableContext, context)
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

        internal static EasyTableContext CreateContext(EasyTablesConfiguration config, EasyTableAttribute attribute, INameResolver resolver)
        {
            Uri resolvedMobileAppUri = config.MobileAppUri;
            string resolvedApiKey = config.ApiKey;

            // Override the config Uri with value from the attribute, if present.
            if (!string.IsNullOrEmpty(attribute.MobileAppUri))
            {
                string uriString = EasyTablesConfiguration.GetSettingFromConfigOrEnvironment(attribute.MobileAppUri);
                resolvedMobileAppUri = new Uri(uriString);
            }

            // If the attribute specifies an empty string ApiKey, set the ApiKey to null.
            if (attribute.ApiKey == string.Empty)
            {
                resolvedApiKey = null;
            }
            else if (attribute.ApiKey != null)
            {
                resolvedApiKey = EasyTablesConfiguration.GetSettingFromConfigOrEnvironment(attribute.ApiKey);
            }

            return new EasyTableContext
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