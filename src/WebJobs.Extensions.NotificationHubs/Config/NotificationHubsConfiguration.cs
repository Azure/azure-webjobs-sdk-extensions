// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Configuration;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    /// <summary>
    /// Defines the configuration options for the NotificationHubs binding.
    /// </summary>
    public class NotificationHubsConfiguration : IExtensionConfigProvider
    {
        internal const string NotificationHubConnectionStringName = "AzureWebJobsNotificationHubsConnectionString";
        internal const string NotificationHubSettingName = "AzureWebJobsNotificationHubName";
        private string _defaultConnectionString;
        private string _defaultHubName;

        /// <summary>
        /// Gets or sets the NotificationHubs ConnectionString to use with the Mobile App.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets NotificationHubs HubName to use with the MobileApp
        /// </summary>
        public string HubName { get; set; }

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            INameResolver nameResolver = context.Config.NameResolver;
            IExtensionRegistry extensions = context.Config.GetService<IExtensionRegistry>();
            _defaultConnectionString = nameResolver.Resolve(NotificationHubConnectionStringName);
            _defaultHubName = nameResolver.Resolve(NotificationHubSettingName);

            var converterManager = context.Config.GetService<IConverterManager>();
            converterManager.AddNotificationHubConverters();

            var bindingFactory = new BindingFactory(nameResolver, converterManager);
            var ruleOutput = bindingFactory.BindToAsyncCollector<NotificationHubAttribute, Notification>(BuildFromAttribute);
            extensions.RegisterBindingRules<NotificationHubAttribute>(ruleOutput);
        }

        private IAsyncCollector<Notification> BuildFromAttribute(NotificationHubAttribute attribute)
        {
            string resolvedConnectionString = ResolveConnectionString(attribute.ConnectionStringSetting);
            string resolvedHubName = ResolveHubName(attribute.HubName);

            INotificationHubClientService service = new NotificationHubClientService(resolvedConnectionString, resolvedHubName);
            return new NotificationHubAsyncCollector(service, attribute.TagExpression);
        }

        /// <summary>
        /// If the attribute ConnectionString is not null or empty, the value is looked up in ConnectionStrings, 
        /// AppSettings, and Environment variables, in that order. Otherwise, the config ConnectionString is
        /// returned.
        /// </summary>
        /// <param name="attributeConnectionString">The connection string from the <see cref="NotificationHubAttribute"/>.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Returns the attributeHubName, as-is, if it is not null or empty. Because the HubName is not considered
        /// a secret, it can be passed as a string literal without requiring an app setting lookup.
        /// </summary>
        /// <param name="attributeHubName">The hub name from the <see cref="NotificationHubAttribute"/>.</param>
        /// <returns></returns>
        internal string ResolveHubName(string attributeHubName)
        {
            // First, try the Attribute's string.
            if (!string.IsNullOrEmpty(attributeHubName))
            {
                return attributeHubName;
            }

            // Second, try the config's HubName
            if (!string.IsNullOrEmpty(HubName))
            {
                return HubName;
            }

            // Finally, fall back to the default.
            return _defaultHubName;
        }
    }
}
