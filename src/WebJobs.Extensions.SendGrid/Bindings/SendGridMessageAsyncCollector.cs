// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using SendGrid;

namespace Microsoft.Azure.WebJobs.Extensions.Bindings
{
    internal class SendGridMessageAsyncCollector : IAsyncCollector<SendGridMessage>
    {
        private readonly SendGridConfiguration _config;
        private readonly SendGridAttribute _attribute;
        private readonly Collection<SendGridMessage> _messages = new Collection<SendGridMessage>();
        private readonly Web _sendGrid;

        public SendGridMessageAsyncCollector(SendGridConfiguration config, SendGridAttribute attribute, Web sendGrid)
        {
            _config = config;
            _attribute = attribute;
            _sendGrid = sendGrid;
        }

        public Task AddAsync(SendGridMessage item, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            SendGridHelpers.DefaultMessageProperties(item, _config, _attribute);

            if (item.To == null || item.To.Length == 0)
            {
                throw new InvalidOperationException("A 'To' address must be specified for the message.");
            }
            if (item.From == null || string.IsNullOrEmpty(item.From.Address))
            {
                throw new InvalidOperationException("A 'From' address must be specified for the message.");
            }

            _messages.Add(item);

            return Task.FromResult(0);
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            foreach (var message in _messages)
            {
                await _sendGrid.DeliverAsync(message);
            }
        }
    }
}
