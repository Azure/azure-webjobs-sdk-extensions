// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.NotificationHubs;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs.Bindings
{
    internal class NotificationHubDirectSendClientBuilder : IConverter<NotificationHubDirectSendAttribute, NotificationHubClient>
    {
        private NotificationHubsConfiguration _config;

        public NotificationHubDirectSendClientBuilder(NotificationHubsConfiguration config)
        {
            _config = config;
        }

        public NotificationHubClient Convert(NotificationHubDirectSendAttribute attribute)
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
