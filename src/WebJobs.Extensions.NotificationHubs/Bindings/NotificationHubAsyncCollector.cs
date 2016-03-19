﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    internal class NotificationHubAsyncCollector : IAsyncCollector<Notification>
    {
        private INotificationHubClientService _notificationHubclientService;
        private string _tagExpression;

        public NotificationHubAsyncCollector(INotificationHubClientService clientService, string tagExpression)
        {
            _notificationHubclientService = clientService;
            _tagExpression = tagExpression;
        }

        public async Task AddAsync(Notification item, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _notificationHubclientService.SendNotificationAsync(item, _tagExpression);
        }

        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(0);
        }
    }
}
