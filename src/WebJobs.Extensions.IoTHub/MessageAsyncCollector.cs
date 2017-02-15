// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;

namespace Microsoft.Azure.WebJobs.Extensions.IoTHub
{
    internal class MessageAsyncCollector : IAsyncCollector<Message>
    {
        private ServiceClient _client;
        private string _deviceId;

        public MessageAsyncCollector(ServiceClient client, string deviceId)
        {
            _client = client;
            _deviceId = deviceId;
        }

        public Task AddAsync(Message item, CancellationToken cancellationToken = default(CancellationToken))
        {
            return _client.SendAsync(_deviceId, item);
        }

        public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }
    }
}
