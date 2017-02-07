// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.NotificationHubs;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs.Bindings
{
    internal class NotificationHubClientBuilder : IConverter<NotificationHubAttribute, NotificationHubClient>
    {
        private NotificationHubsConfiguration _config;

        public NotificationHubClientBuilder(NotificationHubsConfiguration config)
        {
            _config = config;
        }

        public NotificationHubClient Convert(NotificationHubAttribute attribute)
        {
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            string resolvedConnectionString = _config.ResolveConnectionString(attribute.ConnectionStringSetting);
            string resolvedHubName = _config.ResolveHubName(attribute.HubName);
            INotificationHubClientService service = _config.GetService(resolvedConnectionString, resolvedHubName, attribute.EnableTestSend);

            return service.GetNotificationHubClient();
        }
    }
}
