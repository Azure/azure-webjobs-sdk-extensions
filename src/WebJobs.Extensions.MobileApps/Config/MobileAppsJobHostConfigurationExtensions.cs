// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.MobileApps;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods for Mobile App integration.
    /// </summary>
    public static class MobileAppsJobHostConfigurationExtensions
    {
        /// <summary>
        /// Enables use of the Mobile App extensions.
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        /// <param name="mobileAppsConfig">The <see cref="MobileAppsConfiguration"/> to use.</param>
        public static void UseMobileApps(this JobHostConfiguration config, MobileAppsConfiguration mobileAppsConfig = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            if (mobileAppsConfig == null)
            {
                mobileAppsConfig = new MobileAppsConfiguration();
            }

            config.RegisterExtensionConfigProvider(mobileAppsConfig);
        }
    }
}