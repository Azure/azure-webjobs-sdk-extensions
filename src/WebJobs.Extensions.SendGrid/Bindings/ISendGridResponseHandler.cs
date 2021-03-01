// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using SendGrid;

namespace Microsoft.Azure.WebJobs.Extensions.Bindings
{
    /// <summary>
    /// A handler of responses received from APIs call to SendGrid.
    /// </summary>
    public interface ISendGridResponseHandler
    {
        /// <summary>
        /// Handles the response.
        /// </summary>
        /// <param name="response">The response to handle.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task.</returns>
        Task HandleAsync(Response response, CancellationToken cancellationToken);
    }
}
