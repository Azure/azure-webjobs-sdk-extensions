// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.NotificationHubs;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    /// <summary>
    /// Describes a direct send notification
    /// </summary>
    public class DirectNotification
    {
        /// <summary>
        /// Notification
        /// </summary>
        public Notification Notification { get; set; }

        /// <summary>
        /// Handle of device
        /// </summary>
        public string DeviceHandle { get; set; }
    }
}
