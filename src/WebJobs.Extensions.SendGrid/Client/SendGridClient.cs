// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
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

        public async Task<Response> SendMessageAsync(SendGridMessage msg, CancellationToken cancellationToken = default(CancellationToken))
        {
            var response = await _client.SendEmailAsync(msg, cancellationToken);

            if ((int)response.StatusCode >= 300)
            {
                string body = await response.Body.ReadAsStringAsync();
                throw new InvalidOperationException(body);
            }

            return response;
        }
    }
}
