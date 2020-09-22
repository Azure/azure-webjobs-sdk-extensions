// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Client
{
    internal class SendGridClient : ISendGridClient
    {
        private SendGrid.SendGridClient _client;

        public SendGridClient(string apiKey)
        {
            _client = new SendGrid.SendGridClient(apiKey);
        }

        public Task<Response> SendMessageAsync(SendGridMessage msg, CancellationToken cancellationToken = default)
        {
            return _client.SendEmailAsync(msg, cancellationToken);
        }
    }
}
