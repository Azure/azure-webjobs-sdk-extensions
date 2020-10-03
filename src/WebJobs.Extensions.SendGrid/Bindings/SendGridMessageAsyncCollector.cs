// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.SendGrid.Config;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Microsoft.Azure.WebJobs.Extensions.SendGrid.Bindings
{
    internal class SendGridMessageAsyncCollector : IAsyncCollector<SendGridMessage>
    {
        private readonly SendGridOptions _options;
        private readonly SendGridAttribute _attribute;
        private readonly ConcurrentQueue<SendGridMessage> _messages = new ConcurrentQueue<SendGridMessage>();
        private readonly ISendGridClient _sendGrid;
        private readonly ISendGridResponseHandler _responseHandler;

        public SendGridMessageAsyncCollector(SendGridOptions options, SendGridAttribute attribute, ISendGridClient sendGrid, ISendGridResponseHandler responseHandler)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
            _sendGrid = sendGrid ?? throw new ArgumentNullException(nameof(sendGrid));
            _responseHandler = responseHandler ?? throw new ArgumentNullException(nameof(responseHandler));
        }

        public Task AddAsync(SendGridMessage item, CancellationToken cancellationToken = default)
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

        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            while (_messages.TryDequeue(out SendGridMessage message))
            {
                Response response = await _sendGrid.SendEmailAsync(message, cancellationToken);

                await _responseHandler.HandleAsync(response, cancellationToken);
            }
        }        
    }
}
