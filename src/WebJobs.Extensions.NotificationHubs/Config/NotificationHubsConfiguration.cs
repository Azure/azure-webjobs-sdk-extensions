// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Configuration;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    /// <summary>
    /// Defines the configuration options for the NotificationHubs binding.
    /// </summary>
    public class NotificationHubsConfiguration : IExtensionConfigProvider
    {
        internal const string NotificationHubConnectionStringName = "AzureWebJobsNotificationHubConnectionString";
        internal const string NotificationHubSettingName = "AzureWebJobsNotificationHubName";

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public NotificationHubsConfiguration()
        {
            if (ConfigurationManager.ConnectionStrings[NotificationHubConnectionStringName] != null)
            {
                ConnectionString = ConfigurationManager.ConnectionStrings[NotificationHubConnectionStringName].ConnectionString;
            }
            if (string.IsNullOrEmpty(ConnectionString))
            {
                ConnectionString = Environment.GetEnvironmentVariable(NotificationHubConnectionStringName);
            }

            HubName = ConfigurationManager.AppSettings[NotificationHubSettingName];
            if (string.IsNullOrEmpty(HubName))
            {
                HubName = Environment.GetEnvironmentVariable(NotificationHubSettingName);
            }
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
            converterManager.AddConverter<TemplateNotification, Notification>(templateNotification => templateNotification);
            converterManager.AddConverter<string, Notification>(messageProperties => BuildTemplateNotificationFromJson(messageProperties));

            var provider = new NotificationHubsAttributeBindingProvider(context.Config.NameResolver, converterManager, this);
            context.Config.RegisterBindingExtension(provider);
        }

        private TemplateNotification BuildTemplateNotificationFromJson(string messageProperties)
        {
            JObject message=JsonConvert.DeserializeObject<JObject>(messageProperties);
            Dictionary<string, string> templateProperties = new Dictionary<string, string>();

            foreach (JProperty property in message.Properties())
            {
                templateProperties.Add(property.Name.ToString(), property.Value.ToString());
            }
            return new TemplateNotification(templateProperties);
        }
    }
}
