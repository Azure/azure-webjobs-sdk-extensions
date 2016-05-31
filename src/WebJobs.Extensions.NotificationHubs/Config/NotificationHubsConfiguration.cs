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

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public NotificationHubsConfiguration()
        {
            ConnectionString = GetSettingFromConfigOrEnvironment(NotificationHubConnectionStringName);
            HubName = GetSettingFromConfigOrEnvironment(NotificationHubSettingName);
        }

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
            var converterManager = context.Config.GetService<IConverterManager>();
            converterManager.AddNotificationHubConverters();

            var bindingFactory = new BindingFactory(nameResolver, converterManager);
            var ruleOutput = bindingFactory.BindToAsyncCollector<NotificationHubAttribute, Notification>(BuildFromAttribute);
            extensions.RegisterBindingRules<NotificationHubAttribute>(ruleOutput);
        }

        private IAsyncCollector<Notification> BuildFromAttribute(NotificationHubAttribute attribute)
        {
            string resolvedConnectionString = ResolveConnectionString(ConnectionString, attribute.ConnectionStringSetting);
            string resolvedHubName = ResolveHubName(HubName, attribute.HubName);

            INotificationHubClientService service = new NotificationHubClientService(resolvedConnectionString, resolvedHubName);
            return new NotificationHubAsyncCollector(service, attribute.TagExpression);
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
                return GetSettingFromConfigOrEnvironment(attributeConnectionString);
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

        internal static string GetSettingFromConfigOrEnvironment(string key)
        {
            string value = null;

            if (string.IsNullOrEmpty(value))
            {
                ConnectionStringSettings connectionString = ConfigurationManager.ConnectionStrings[key];
                if (connectionString != null)
                {
                    value = connectionString.ConnectionString;
                }

                if (string.IsNullOrEmpty(value))
                {
                    value = ConfigurationManager.AppSettings[key];
                }

                if (string.IsNullOrEmpty(value))
                {
                    value = Environment.GetEnvironmentVariable(key);
                }
            }

            return value;
        }
    }
}
