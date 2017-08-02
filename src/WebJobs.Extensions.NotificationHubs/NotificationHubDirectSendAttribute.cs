// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.NotificationHubs;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to bind a parameter to an Azure NotificationHub
    /// </summary>
    /// <remarks>
    /// The method parameter type can be one of the following:
    /// <list type="bullet">
    /// <item><description><see cref="IAsyncCollector{T}"/>, where T is a <see cref="DirectNotification"/>.</description></item>
    /// </list>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    [Binding]
    public sealed class NotificationHubDirectSendAttribute : Attribute
    {
        /// <summary>
        /// Optional. A string value indicating the app setting to use as the Notification Hubs connection
        /// string, if different than the one specified in the <see cref="NotificationHubsConfiguration"/>.
        /// </summary>
        [AppSetting]
        public string ConnectionStringSetting { get; set; }

        /// <summary>
        /// Optional. The Notification Hub Name to use, if different than the one specified in the
        /// <see cref="NotificationHubsConfiguration"/>.
        /// </summary>
        public string HubName { get; set; }

        /// <summary>
        /// Optional. Boolean value to enable debug send on NotificationHubClient
        /// <see cref="NotificationHubClient"/>.
        /// </summary>
        public bool EnableTestSend { get; set; }
    }
}
