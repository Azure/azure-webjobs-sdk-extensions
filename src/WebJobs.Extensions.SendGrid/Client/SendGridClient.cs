// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using SendGrid;
using SendGrid.CSharp.HTTP.Client;

namespace Microsoft.Azure.WebJobs.Extensions.Client
{
    internal class SendGridClient : ISendGridClient
    {
        private SendGridAPIClient _sendGrid;

        public SendGridClient(string apiKey)
        {
            _sendGrid = new SendGridAPIClient(apiKey);
        }

        public async Task SendMessageAsync(string mailJson)
        {
            Response response = await _sendGrid.client.mail.send.post(requestBody: mailJson);
            if ((int)response.StatusCode >= 300)
            {
                string body = await response.Body.ReadAsStringAsync();
                throw new InvalidOperationException(body);
            }
        }
    }
}
