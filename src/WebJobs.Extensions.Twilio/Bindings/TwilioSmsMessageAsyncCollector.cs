// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Twilio;

namespace Microsoft.Azure.WebJobs.Extensions.Bindings
{
    internal class TwilioSmsMessageAsyncCollector : IAsyncCollector<SMSMessage>
    {
        private readonly TwilioSmsContext _context;
        private readonly Collection<SMSMessage> _messages = new Collection<SMSMessage>();

        public TwilioSmsMessageAsyncCollector(TwilioSmsContext context)
        {
            _context = context;
        }

        public Task AddAsync(SMSMessage item, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            ApplyContextMessageSettings(item, _context);

            if (string.IsNullOrEmpty(item.To))
            {
                throw new InvalidOperationException("A 'To' number must be specified for the message.");
            }

            if (string.IsNullOrEmpty(item.From))
            {
                throw new InvalidOperationException("A 'From' number must be specified for the message.");
            }

            if (string.IsNullOrEmpty(item.Body))
            {
                throw new InvalidOperationException("A 'Body' must be specified for the message.");
            }

            _messages.Add(item);

            return Task.FromResult(0);
        }

        internal static void ApplyContextMessageSettings(SMSMessage message, TwilioSmsContext context)
        {
            message.From = Utility.FirstOrDefault(message.From, context.From);
            message.To = Utility.FirstOrDefault(message.To, context.To);
            message.Body = Utility.FirstOrDefault(message.Body, context.Body);
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await Task.Run(() =>
            {
                foreach (var message in _messages)
                {
                    Message response = _context.Client.SendMessage(message.From, message.To, message.Body);

                    if (response.RestException != null)
                    {
                        WebExceptionStatus status = (WebExceptionStatus)int.Parse(response.RestException.Status);
                        throw new WebException(response.RestException.Message, status);
                    }
                }
            });
        }
    }
}
