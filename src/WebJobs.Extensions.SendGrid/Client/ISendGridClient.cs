// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Client
{
    internal interface ISendGridClient
    {
        Task<Response> SendMessageAsync(SendGridMessage msg, CancellationToken cancellationToken = default(CancellationToken));
    }
}
