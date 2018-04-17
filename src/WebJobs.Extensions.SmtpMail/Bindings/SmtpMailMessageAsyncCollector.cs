// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Client;
using Microsoft.Azure.WebJobs.Extensions.SmtpMail;

namespace Microsoft.Azure.WebJobs.Extensions.Bindings
{
    internal class SmtpMailMessageAsyncCollector : IAsyncCollector<MailMessage>
    {
        private readonly SmtpMailConfiguration _config;
        private readonly SmtpMailAttribute _attribute;
        private readonly List<MailMessage> _messages = new List<MailMessage>();
        private readonly ISmtpMailClient _smtpMailClient;

        public SmtpMailMessageAsyncCollector(SmtpMailConfiguration config, SmtpMailAttribute attribute, ISmtpMailClient smtpMailClient)
        {
            _config = config;
            _attribute = attribute;
            _smtpMailClient = smtpMailClient;
        }

        public Task AddAsync(MailMessage message, CancellationToken cancellationToken = default)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            SmtpMailHelpers.DefaultMessageProperties(message, _config, _attribute);

            if (!SmtpMailHelpers.IsToValid(message))
            {
                throw new InvalidOperationException($"A '{nameof(message.To)}' address must be specified for the message.");
            }
            if (!SmtpMailHelpers.IsFromValid(message))
            {
                throw new InvalidOperationException($"A '{nameof(message.From)}' address must be specified for the message.");
            }

            _messages.Add(message);

            return Task.CompletedTask;
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            await _smtpMailClient.SendMessagesAsync(_messages, cancellationToken);
            _messages.Clear();
        }
    }
}
