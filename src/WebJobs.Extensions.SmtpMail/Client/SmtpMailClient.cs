// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Config;

namespace Client
{
    internal class SmtpMailClient : ISmtpMailClient
    {
        private readonly string _connectionString;

        public SmtpMailClient(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task SendMessagesAsync(IList<MailMessage> messages, CancellationToken cancellationToken)
        {
            using (var client = SmtpMailConnectionParser.Parse(_connectionString))
            {
                for (int i = 0, len = messages.Count; i < len; i++)
                {
                    var message = messages[i];
                    cancellationToken.ThrowIfCancellationRequested();
                    if (message != null)
                    {
                        await client.SendMailAsync(message);
                        message.Dispose();
                        messages[i] = null;
                    }
                }
            }
        }
    }
}
