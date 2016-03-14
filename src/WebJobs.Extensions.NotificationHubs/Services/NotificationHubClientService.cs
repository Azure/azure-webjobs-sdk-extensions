// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    internal class NotificationHubClientService : INotificationHubClientService
    {
        private NotificationHubClient _nhClient;
        public NotificationHubClientService(NotificationHubsConfiguration nhConfig)
        {
            _nhClient = NotificationHubClient.CreateClientFromConnectionString(nhConfig.ConnectionString, nhConfig.HubName);
        }
        public async Task<NotificationOutcome> SendNotificationAsync(Notification notification, string tagExpression)
        {
            var notificationOutcome = await _nhClient.SendNotificationAsync(notification, tagExpression);
            return notificationOutcome;
        }
    }
}
