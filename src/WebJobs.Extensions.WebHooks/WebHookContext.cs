// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;

namespace Microsoft.Azure.WebJobs.Extensions.WebHooks
{
    /// <summary>
    /// A class providing execution context for a WebHook function invocations.
    /// </summary>
    public class WebHookContext
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="request">The <see cref="HttpRequestMessage"/> that initiated
        /// the WebHook invocation.</param>
        public WebHookContext(HttpRequestMessage request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            Request = request;
        }

        /// <summary>
        /// The <see cref="HttpRequestMessage"/> that triggered the WebHook invocation.
        /// </summary>
        public HttpRequestMessage Request { get; private set; }

        /// <summary>
        /// The optional <see cref="HttpResponseMessage"/> to return from the WebHook invocation.
        /// When not set by the function, a response will be automatically generated and returned
        /// based on the success/failure of the function.
        /// </summary>
        public HttpResponseMessage Response { get; set; }
    }
}
