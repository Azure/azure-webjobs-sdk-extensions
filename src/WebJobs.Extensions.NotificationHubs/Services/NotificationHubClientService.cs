// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    internal class NotificationHubClientService : INotificationHubClientService
    {
        private string _connectionString;
        private string _hubName;
        private NotificationHubClient _notificationHubClient;

        public NotificationHubClientService(string connectionString, string hubName, bool enableTestSend = false)
        {
            _connectionString = connectionString;
            _hubName = hubName;
            _notificationHubClient = NotificationHubClient.CreateClientFromConnectionString(_connectionString, _hubName, enableTestSend);
        }

        public Task<NotificationOutcome> SendNotificationAsync(Notification notification, string tagExpression)
        {
            return _notificationHubClient.SendNotificationAsync(notification, tagExpression);
        }

        public Task<NotificationOutcome> SendDirectNotificationAsync(Notification notification, string deviceHandle)
        {
            return _notificationHubClient.SendDirectNotificationAsync(notification, deviceHandle);
        }

        public NotificationHubClient GetNotificationHubClient()
        {
            return _notificationHubClient;
        }
    }
}