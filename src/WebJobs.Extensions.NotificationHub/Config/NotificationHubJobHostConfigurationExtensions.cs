// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.NotificationHubs;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods for NotificationHubs integration.
    /// </summary>
    public static class NotificationHubJobHostConfigurationExtensions
    {
        /// <summary>
        /// Enables use of NotificationHubs extenstion
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        /// <param name="notificationHubsConfig">The <see cref="NotificationHubConfiguration"/>to use</param>
        public static void UseNotificationHubs(this JobHostConfiguration config, NotificationHubConfiguration notificationHubsConfig = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            if (notificationHubsConfig == null)
            {
                notificationHubsConfig = new NotificationHubConfiguration();
            }
            config.RegisterExtensionConfigProvider(notificationHubsConfig);
        }
    }
}
