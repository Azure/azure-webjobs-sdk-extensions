// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    internal class NotificationHubAttributeBindingProvider : IBindingProvider
    {
        private readonly IConverterManager _converterManager;
        private NotificationHubsConfiguration _config;

        public NotificationHubAttributeBindingProvider(IConverterManager converterManager, NotificationHubsConfiguration config)
        {
            _converterManager = converterManager;
            _config = config;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            NotificationHubAttribute attribute = parameter.GetCustomAttribute<NotificationHubAttribute>(inherit: false);
            if (attribute == null)
            {
                return Task.FromResult<IBinding>(null);
            }

            Converter.NotificationPlatform = attribute.Platform;

            if (string.IsNullOrEmpty(_config.ConnectionString) &&
                string.IsNullOrEmpty(attribute.ConnectionString))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture,
                    "The Notification Hub connection string must be set either via a '{0}' app setting, via the NotificationHubAttribute.ConnectionString property or via NotificationHubsConfiguration.ConnectionString.",
                    NotificationHubsConfiguration.NotificationHubConnectionStringName));
            }

            if (string.IsNullOrEmpty(_config.HubName) &&
                string.IsNullOrEmpty(attribute.HubName))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture,
                    "The Notification Hub hub name must be set either via a '{0}' app setting, via the NotificationHubAttribute.HubName property or via NotificationHubsConfiguration.HubName.",
                    NotificationHubsConfiguration.NotificationHubSettingName));
            }

            string resolvedConnectionString = ResolveConnectionString(_config.ConnectionString, attribute.ConnectionString);
            string resolvedHubName = ResolveHubName(_config.HubName, attribute.HubName);

            INotificationHubClientService service = new NotificationHubClientService(resolvedConnectionString, resolvedHubName);

            Func<string, INotificationHubClientService> invokeStringBinder = (invokeString) => service;

            IBinding binding = BindingFactory.BindCollector(
                parameter,
                _converterManager,
                (nhClientService, valueBindingContext) => new NotificationHubAsyncCollector(service, attribute.TagExpression),
                "NotificationHubs",
                invokeStringBinder);

            return Task.FromResult(binding);
        }

        /// <summary>
        /// If the attribute ConnectionString is not null or empty, the value is looked up in ConnectionStrings, 
        /// AppSettings, and Environment variables, in that order. Otherwise, the config ConnectionString is
        /// returned.
        /// </summary>
        /// <param name="configConnectionString">The connection string from the <see cref="NotificationHubsConfiguration"/>.</param>
        /// <param name="attributeConnectionString">The connection string from the <see cref="NotificationHubAttribute"/>.</param>
        /// <returns></returns>
        internal static string ResolveConnectionString(string configConnectionString, string attributeConnectionString)
        {
            if (!string.IsNullOrEmpty(attributeConnectionString))
            {
                return NotificationHubsConfiguration.GetSettingFromConfigOrEnvironment(attributeConnectionString);
            }
            return configConnectionString;
        }

        /// <summary>
        /// Returns the attributeHubName, as-is, if it is not null or empty. Because the HubName is not considered
        /// a secret, it can be passed as a string literal without requiring an app setting lookup.
        /// </summary>
        /// <param name="configHubName">The hub name from the <see cref="NotificationHubsConfiguration"/>.</param>
        /// <param name="attributeHubName">The hub name from the <see cref="NotificationHubAttribute"/>.</param>
        /// <returns></returns>
        internal static string ResolveHubName(string configHubName, string attributeHubName)
        {
            if (!string.IsNullOrEmpty(attributeHubName))
            {
                return attributeHubName;
            }
            return configHubName;
        }
    }
}
