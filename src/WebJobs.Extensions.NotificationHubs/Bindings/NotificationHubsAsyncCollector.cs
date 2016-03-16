﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;

namespace Microsoft.Azure.WebJobs.Extensions.NotificationHubs
{
    internal class NotificationHubsAsyncCollector : IAsyncCollector<Notification>
    {
        INotificationHubClientService _nhClientService;
        string _tagExpression;

        public NotificationHubsAsyncCollector(INotificationHubClientService nhClientService, string tagExpression)
        {
            _nhClientService = nhClientService;
            _tagExpression = tagExpression;
        }

        public async Task AddAsync(Notification item, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _nhClientService.SendNotificationAsync(item, _tagExpression);
        }

        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(0);
        }
    }
}
