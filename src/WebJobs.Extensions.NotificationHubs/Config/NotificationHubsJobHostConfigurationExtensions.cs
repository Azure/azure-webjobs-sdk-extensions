// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    /// <summary>
    /// Extension methods for NotificationHubs integration.
    /// </summary>
    public static class NotificationHubsJobHostConfigurationExtensions
    {
        /// <summary>
        /// Enables use of NotificationHubs extenstion
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        /// <param name="notificationHubsConfig">The <see cref="NotificationHubsConfiguration"/>to use</param>
        public static void UseNotificationHubs(this JobHostConfiguration config, NotificationHubsConfiguration notificationHubsConfig = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            if (notificationHubsConfig == null)
            {
                notificationHubsConfig = new NotificationHubsConfiguration();
            }
            config.RegisterExtensionConfigProvider(notificationHubsConfig);
        }
    }
}
