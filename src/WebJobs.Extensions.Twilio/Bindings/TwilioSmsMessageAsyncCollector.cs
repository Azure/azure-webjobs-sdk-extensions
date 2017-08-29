// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Microsoft.Azure.WebJobs.Extensions.Bindings
{
    internal class TwilioSmsMessageAsyncCollector : IAsyncCollector<CreateMessageOptions>
    {
        private readonly TwilioSmsContext _context;
        private readonly Collection<CreateMessageOptions> _messageOptionsCollection = new Collection<CreateMessageOptions>();

        public TwilioSmsMessageAsyncCollector(TwilioSmsContext context)
        {
            _context = context;
        }

        public Task AddAsync(CreateMessageOptions messageOptions, CancellationToken cancellationToken = default(CancellationToken))
        {
            ApplyContextMessageSettings(messageOptions, _context);

            if (messageOptions.To == null)
            {
                throw new InvalidOperationException("A 'To' number must be specified for the message.");
            }

            if (messageOptions.From == null)
            {
                throw new InvalidOperationException("A 'From' number must be specified for the message.");
            }

            if (messageOptions.Body == null)
            {
                throw new InvalidOperationException("A 'Body' must be specified for the message.");
            }

            _messageOptionsCollection.Add(messageOptions);

            return Task.CompletedTask;
        }

        internal static void ApplyContextMessageSettings(CreateMessageOptions messageOptions, TwilioSmsContext context)
        {
            if (messageOptions.From == null)
            {
                messageOptions.From = new PhoneNumber(context.From);
            }

            if (messageOptions.Body == null)
            {
                messageOptions.Body = context.Body;
            }
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            foreach (var message in _messageOptionsCollection)
            {
                // this create will initiate the send operation
                await MessageResource.CreateAsync(message, client: _context.Client);
            }
        }
    }
}
