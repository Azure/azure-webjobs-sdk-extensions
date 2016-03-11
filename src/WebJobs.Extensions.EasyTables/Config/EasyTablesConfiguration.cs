// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Configuration;
using Microsoft.Azure.WebJobs.Host.Config;

namespace Microsoft.Azure.WebJobs.Extensions.EasyTables
{
    /// <summary>
    /// Defines the configuration options for the EasyTable binding.
    /// </summary>
    public class EasyTablesConfiguration : IExtensionConfigProvider
    {
        internal const string AzureWebJobsMobileAppUriName = "AzureWebJobsMobileAppUri";
        internal const string AzureWebJobsMobileAppApiKeyName = "AzureWebJobsMobileAppApiKey";

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public EasyTablesConfiguration()
        {
            this.ApiKey = Resolve(AzureWebJobsMobileAppApiKeyName);

            string uriString = Resolve(AzureWebJobsMobileAppUriName);

            // if not found, MobileAppUri must be set explicitly before using the config
            if (!string.IsNullOrEmpty(uriString))
            {
                this.MobileAppUri = new Uri(uriString);
            }

            this.ClientFactory = new DefaultMobileServiceClientFactory();
        }

        /// <summary>
        /// Gets or sets the ApiKey to use with the Mobile App.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the Mobile App URI for the Easy Table.
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

            context.Config.RegisterBindingExtension(
                new EasyTableAttributeBindingProvider(context.Config, this, context.Config.NameResolver));
        }

        internal static string Resolve(string key)
        {
            string value = null;

            if (string.IsNullOrEmpty(value))
            {
                value = ConfigurationManager.AppSettings[key];
                if (string.IsNullOrEmpty(value))
                {
                    value = Environment.GetEnvironmentVariable(key);
                }
            }

            return value;
        }
    }
}