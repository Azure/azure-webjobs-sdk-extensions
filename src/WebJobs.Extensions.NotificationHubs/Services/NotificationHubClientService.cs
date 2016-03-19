// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    internal class NotificationHubClientService : INotificationHubClientService
    {
        private NotificationHubsConfiguration _notificationHubClientConfig;
        private NotificationHubClient _notificationHubClient;
        public NotificationHubClientService(NotificationHubsConfiguration notificationHubConfig)
        {
            _notificationHubClientConfig = notificationHubConfig;
        }
        public async Task<NotificationOutcome> SendNotificationAsync(Notification notification, string tagExpression)
        {
            var notificationOutcome = await GetNotificationHubClient().SendNotificationAsync(notification, tagExpression);
            return notificationOutcome;
        }

        private NotificationHubClient GetNotificationHubClient()
        {
            if (_notificationHubClient == null)
            {
                _notificationHubClient = NotificationHubClient.CreateClientFromConnectionString(_notificationHubClientConfig.ConnectionString, _notificationHubClientConfig.HubName);
            }
            return _notificationHubClient;
        }
    }
}
