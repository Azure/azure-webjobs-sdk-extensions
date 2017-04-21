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
    /// <item><description><see cref="ICollector{T}"/>, where T is either <see cref="TemplateNotification"/> or <see cref="Notification"/>.</description></item>
    /// <item><description><see cref="IAsyncCollector{T}"/><see cref="TemplateNotification"/> or <see cref="Notification"/>.</description></item>
    /// <item><description>out T, where T is either <see cref="TemplateNotification"/> or <see cref="Notification"/>.</description></item>
    /// <item><description>out T[], where T is either <see cref="TemplateNotification"/> or <see cref="Notification"/>.</description></item>
    /// <item><description>out string, valid JSON string with template properties to build <see cref="TemplateNotification"/></description></item>
    /// <item><description>out IDictionary, string key value pairs of templateProperties to build <see cref="TemplateNotification"/></description></item>
    /// </list>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    [Binding]
    public sealed class NotificationHubAttribute : Attribute
    {
        /// <summary>
        /// Optional. A tag expression is any boolean expression constructed using the logical operator
        /// </summary>
        [AutoResolve]
        public string TagExpression { get; set; }

        /// <summary>
        /// Optional. Specify platform for sending native notifications.<see cref="NotificationPlatform"/>.
        /// </summary>
        public NotificationPlatform Platform { get; set; }

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
