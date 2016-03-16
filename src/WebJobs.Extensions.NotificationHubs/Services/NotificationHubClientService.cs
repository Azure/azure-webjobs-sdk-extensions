// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    internal class NotificationHubClientService : INotificationHubClientService
    {
        private NotificationHubsConfiguration _nhClientConfig;
        private NotificationHubClient _nhClient;
        public NotificationHubClientService(NotificationHubsConfiguration nhConfig)
        {
            _nhClientConfig = nhConfig;
        }
        public async Task<NotificationOutcome> SendNotificationAsync(Notification notification, string tagExpression)
        {
            var notificationOutcome = await _nhClient.SendNotificationAsync(notification, tagExpression);
            return notificationOutcome;
        }

        private NotificationHubClient GetNotificationHubClient()
        {
            if(_nhClient==null)
            {
                _nhClient = NotificationHubClient.CreateClientFromConnectionString(_nhClientConfig.ConnectionString, _nhClientConfig.HubName);
            }
            return _nhClient;
        }
    }
}
