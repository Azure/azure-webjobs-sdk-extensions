// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Client;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using SendGrid.Helpers.Mail;

namespace Microsoft.Azure.WebJobs.Extensions.Bindings
{
    internal class SendGridMessageAsyncCollector : IAsyncCollector<SendGridMessage>
    {
        private readonly SendGridOptions _options;
        private readonly SendGridAttribute _attribute;
        private readonly ConcurrentQueue<SendGridMessage> _messages = new ConcurrentQueue<SendGridMessage>();
        private readonly ISendGridClient _sendGrid;

        public SendGridMessageAsyncCollector(SendGridOptions options, SendGridAttribute attribute, ISendGridClient sendGrid)
        {
            _options = options;
            _attribute = attribute;
            _sendGrid = sendGrid;
        }

        public Task AddAsync(SendGridMessage item, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            SendGridHelpers.DefaultMessageProperties(item, _options, _attribute);

            if (!SendGridHelpers.IsToValid(item))
            {
                throw new InvalidOperationException("A 'To' address must be specified for the message.");
            }
            if (item.From == null || string.IsNullOrEmpty(item.From.Email))
            {
                throw new InvalidOperationException("A 'From' address must be specified for the message.");
            }

            _messages.Enqueue(item);

            return Task.CompletedTask;
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            while (_messages.TryDequeue(out SendGridMessage message))
            {
                await _sendGrid.SendMessageAsync(message);
            }
        }        
    }
}
