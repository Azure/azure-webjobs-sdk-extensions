﻿using System;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Helper extensions for ApiHub configuration
    /// </summary>
    public static partial class ApiHubJobHostConfigurationExtensions
    {
        /// <summary>
        /// Add ApiHub ocnfiguration to <see cref="JobHostConfiguration"/>
        /// </summary>
        /// <param name="config">curent <see cref="JobHostConfiguration"/></param>
        /// <param name="apiHubConfiguration">Instance of <see cref="ApiHubConfiguration"/></param>
        public static void UseApiHub(this JobHostConfiguration config, ApiHubConfiguration apiHubConfiguration)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            if (apiHubConfiguration == null)
            {
                throw new ArgumentNullException("apiHubConfiguration");
            }
            IExtensionRegistry extensions = config.GetService<IExtensionRegistry>();
            extensions.RegisterExtension<IExtensionConfigProvider>(apiHubConfiguration);
        }
    }
}
