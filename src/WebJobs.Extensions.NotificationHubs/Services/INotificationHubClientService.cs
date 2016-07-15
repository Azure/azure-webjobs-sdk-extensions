// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    /// <summary>
    ///   Defines an interface for NotificationHubClient sendNotification
    /// </summary>
    internal interface INotificationHubClientService
    {
        /// <summary>
        /// Asynchronously sends a notification to a tag expression
        /// </summary>
        /// <param name="notification">notification to send</param>
        /// <param name="tagExpression">A tag expression is any boolean expression constructed using the logical operator</param>
        /// <returns></returns>
        Task<NotificationOutcome> SendNotificationAsync(Notification notification, string tagExpression);

        /// <summary>
        /// Returns the underlying <see cref="NotificationHubClient"/>.
        /// </summary>
        /// <returns></returns>
        NotificationHubClient GetNotificationHubClient();
    }
}
