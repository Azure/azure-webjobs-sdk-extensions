// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.SendGrid.Bindings;
using SendGrid;

namespace Microsoft.Azure.WebJobs.Extensions.SendGrid.Config
{
    /// <summary>
    /// The default handler of responses received from APIs call to SendGrid.
    /// </summary>
    public class DefaultSendGridResponseHandler : ISendGridResponseHandler
    {
        /// <summary>
        /// Handles the response.
        /// </summary>
        /// <param name="response">The response to handle.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task.</returns>
        public async Task HandleAsync(Response response, CancellationToken cancellationToken)
        {
            // https://sendgrid.com/docs/API_Reference/Web_API_v3/Mail/errors.html
            if ((int)response.StatusCode >= 400)
            {
                string body = await response.Body.ReadAsStringAsync();

                throw new InvalidOperationException(body);
            }
        }
    }
}
