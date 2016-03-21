// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Configuration;
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
            var converterManager = context.Config.GetService<IConverterManager>();
            converterManager = converterManager.AddNotificationHubConverters();
            var provider = new NotificationHubAttributeBindingProvider(converterManager, this);
            context.Config.RegisterBindingExtension(provider);
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
