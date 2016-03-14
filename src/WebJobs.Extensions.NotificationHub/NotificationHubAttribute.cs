// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.NotificationHubs;

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
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class NotificationHubAttribute : Attribute
    {
        /// <summary>
        /// A tag expression is any boolean expression constructed using the logical operator
        /// </summary>
        public string TagExpression { get; set; }
    }
}
