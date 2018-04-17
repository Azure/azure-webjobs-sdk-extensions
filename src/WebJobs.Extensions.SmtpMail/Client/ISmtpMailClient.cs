// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace Client
{
    internal interface ISmtpMailClient
    {
        Task SendMessagesAsync(IList<MailMessage> messages, CancellationToken cancellationToken);
    }
}
