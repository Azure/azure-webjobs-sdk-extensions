// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.Http.Middleware
{
    public class HttpThrottleMiddleware
    {
        private readonly RequestDelegate _next;
        private ActionBlock<HttpRequestItem> _requestQueue;

        public HttpThrottleMiddleware(RequestDelegate next, IOptions<HttpOptions> httpOptions, ILoggerFactory loggerFactory)
        {
            _next = next;
            Options = httpOptions.Value;
            Logger = loggerFactory?.CreateLogger("Host.Extensions.Http.HttpThrottleMiddleware");

            if (Options.MaxOutstandingRequests != DataflowBlockOptions.Unbounded ||
                Options.MaxConcurrentRequests != DataflowBlockOptions.Unbounded)
            {
                InitializeRequestQueue();
            }
        }

        /// <summary>
        /// Gets the <see cref="HttpOptions"/>.
        /// </summary>
        protected HttpOptions Options { get; }

        /// <summary>
        /// Gets the <see cref="ILogger"/>.
        /// </summary>
        protected ILogger Logger { get; }

        public virtual async Task Invoke(HttpContext httpContext)
        {
            if (_requestQueue != null)
            {
                // enqueue the request workitem
                var item = new HttpRequestItem
                {
                    HttpContext = httpContext,
                    Next = _next,
                    CompletionSource = new TaskCompletionSource<object>()
                };

                if (_requestQueue.Post(item))
                {
                    await item.CompletionSource.Task;
                }
                else
                {
                    Logger?.LogInformation($"Http request queue limit of {Options.MaxOutstandingRequests} has been exceeded.");
                    RejectRequest(httpContext);
                }
            }
            else
            {
                // queue is not enabled, so just dispatch the request directly
                await _next.Invoke(httpContext);
            }
        }

        /// <summary>
        /// For a request that will be rejected due to load, max queue length
        /// exceeded, etc. this method will be called, allowing the
        /// status code, headers, etc. for the request to be configured.
        /// </summary>
        /// <param name="httpContext">The <see cref="HttpContext"/> for the request.</param>
        protected virtual void RejectRequest(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = 429;
        }

        private void InitializeRequestQueue()
        {
            var options = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = Options.MaxConcurrentRequests,
                BoundedCapacity = Options.MaxOutstandingRequests
            };

            _requestQueue = new ActionBlock<HttpRequestItem>(async item =>
            {
                try
                {
                    await item.Next.Invoke(item.HttpContext);
                    item.CompletionSource.SetResult(null);
                }
                catch (Exception ex)
                {
                    item.CompletionSource.SetException(ex);
                }
            }, options);
        }

        private class HttpRequestItem
        {
            /// <summary>
            /// Gets or sets the request context to process.
            /// </summary>
            public HttpContext HttpContext { get; set; }

            /// <summary>
            /// Gets or sets the completion delegate for the request.
            /// </summary>
            public RequestDelegate Next { get; set; }

            /// <summary>
            /// Gets or sets the completion source to use.
            /// </summary>
            public TaskCompletionSource<object> CompletionSource { get; set; }
        }
    }
}
