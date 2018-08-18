// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Http.Middleware;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Http
{
    public class HttpThrottleMiddlewareTests
    {
        private readonly ILoggerFactory _loggerFactory = new LoggerFactory();
        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        public HttpThrottleMiddlewareTests()
        {
            _loggerFactory.AddProvider(_loggerProvider);
        }

        [Fact]
        public async Task Invoke_PropagatesExceptions()
        {
            var ex = new Exception("Kaboom!");
            bool nextInvoked = false;
            RequestDelegate next = (ctxt) =>
            {
                nextInvoked = true;
                throw ex;
            };
            var options = new HttpOptions
            {
                MaxOutstandingRequests = 10,
                MaxConcurrentRequests = 5
            };
            var middleware = new HttpThrottleMiddleware(next, new OptionsWrapper<HttpOptions>(options), _loggerFactory);

            var resultEx = await Assert.ThrowsAsync<Exception>(async () =>
            {
                var httpContext = new DefaultHttpContext();
                await middleware.Invoke(httpContext);
            });
            Assert.True(nextInvoked);
            Assert.Same(ex, resultEx);
        }

        [Fact]
        public async Task Invoke_NoThrottle_DispatchesDirectly()
        {
            bool nextInvoked = false;
            RequestDelegate next = (ctxt) =>
            {
                nextInvoked = true;
                ctxt.Response.StatusCode = (int)HttpStatusCode.Accepted;
                return Task.CompletedTask;
            };
            var options = new HttpOptions();
            var middleware = new HttpThrottleMiddleware(next, new OptionsWrapper<HttpOptions>(options), _loggerFactory);

            var httpContext = new DefaultHttpContext();
            await middleware.Invoke(httpContext);
            Assert.True(nextInvoked);
            Assert.Equal(HttpStatusCode.Accepted, (HttpStatusCode)httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_MaxParallelism_RequestsAreThrottled()
        {
            int maxParallelism = 3;
            int count = 0;
            RequestDelegate next = async (ctxt) =>
            {
                if (Interlocked.Increment(ref count) > maxParallelism)
                {
                    throw new Exception("Kaboom!");
                }

                await Task.Delay(100);
                Interlocked.Decrement(ref count);
                ctxt.Response.StatusCode = (int)HttpStatusCode.Accepted;
            };

            var options = new HttpOptions
            {
                MaxConcurrentRequests = maxParallelism
            };
            var middleware = new HttpThrottleMiddleware(next, new OptionsWrapper<HttpOptions>(options), _loggerFactory);

            // expect all requests to succeed
            var tasks = new List<Task>();
            var httpContexts = new List<HttpContext>();
            for (int i = 0; i < 20; i++)
            {
                var httpContext = new DefaultHttpContext();
                httpContexts.Add(httpContext);
                tasks.Add(middleware.Invoke(httpContext));
            }
            await Task.WhenAll(tasks);
            Assert.True(httpContexts.All(p => (HttpStatusCode)p.Response.StatusCode == HttpStatusCode.Accepted));
        }

        [Fact]
        public async Task Invoke_MaxOutstandingRequestsExceeded_RequestsAreRejected()
        {
            int maxQueueLength = 10;
            RequestDelegate next = async (ctxt) =>
            {
                await Task.Delay(100);
                ctxt.Response.StatusCode = (int)HttpStatusCode.Accepted;
            };
            
            var options = new HttpOptions
            {
                MaxOutstandingRequests = maxQueueLength,
                MaxConcurrentRequests = 1
            };
            var middleware = new HttpThrottleMiddleware(next, new OptionsWrapper<HttpOptions>(options), _loggerFactory);

            // expect requests past the threshold to be rejected
            var tasks = new List<Task>();
            var httpContexts = new List<HttpContext>();
            for (int i = 0; i < 25; i++)
            {
                var httpContext = new DefaultHttpContext();
                httpContexts.Add(httpContext);
                tasks.Add(middleware.Invoke(httpContext));
            }
            await Task.WhenAll(tasks);
            int countSuccess = httpContexts.Count(p => (HttpStatusCode)p.Response.StatusCode == HttpStatusCode.Accepted);
            Assert.Equal(maxQueueLength, countSuccess);
            int rejectCount = 25 - countSuccess;
            Assert.Equal(rejectCount, httpContexts.Count(p => p.Response.StatusCode == 429));

            IEnumerable<LogMessage> logMessages = _loggerProvider.GetAllLogMessages();
            Assert.Equal(rejectCount, logMessages.Count());
            Assert.True(logMessages.All(p => string.Compare("Http request queue limit of 10 has been exceeded.", p.FormattedMessage) == 0));

            // send a number of requests not exceeding the limit
            // expect all to succeed
            tasks = new List<Task>();
            httpContexts = new List<HttpContext>();
            for (int i = 0; i < maxQueueLength; i++)
            {
                var httpContext = new DefaultHttpContext();
                httpContexts.Add(httpContext);
                tasks.Add(middleware.Invoke(httpContext));
            }
            await Task.WhenAll(tasks);
            Assert.True(httpContexts.All(p => (HttpStatusCode)p.Response.StatusCode == HttpStatusCode.Accepted));
        }

        [Fact]
        public async Task Invoke_HostIsOverloaded_RequestsAreRejected()
        {
            bool rejectRequests = false;
            var options = new HttpOptions();
            Func<bool> rejectAllRequests = () =>
            {
                return rejectRequests;
            };

            RequestDelegate next = async (ctxt) =>
            {
                await Task.Delay(100);
                ctxt.Response.StatusCode = (int)HttpStatusCode.Accepted;
            };
            var middleware = new TestHttpThrottleMiddleware(next, new OptionsWrapper<HttpOptions>(options), rejectAllRequests);

            var tasks = new List<Task>();
            var httpContexts = new List<HttpContext>();
            for (int i = 0; i < 10; i++)
            {
                if (i == 7)
                {
                    rejectRequests = true;
                }
                var httpContext = new DefaultHttpContext();
                httpContexts.Add(httpContext);
                tasks.Add(middleware.Invoke(httpContext));
            }
            await Task.WhenAll(tasks);

            Assert.Equal(7, httpContexts.Count(p => (HttpStatusCode)p.Response.StatusCode == HttpStatusCode.Accepted));
            Assert.Equal(3, httpContexts.Count(p => (HttpStatusCode)p.Response.StatusCode == HttpStatusCode.TooManyRequests));
        }

        [Fact]
        public async Task Invoke_HostIsOverloaded_CustomRejectAction()
        {
            bool rejectOverrideCalled = false;
            var options = new HttpOptions();
            Func<bool> rejectAllRequests = () =>
            {
                return true;
            };
            Action<HttpContext> rejectRequest = (ctxt) =>
            {
                rejectOverrideCalled = true;
                ctxt.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            };

            RequestDelegate next = (ctxt) =>
            {
                ctxt.Response.StatusCode = (int)HttpStatusCode.Accepted;
                return Task.CompletedTask;
            };
            var middleware = new TestHttpThrottleMiddleware(next, new OptionsWrapper<HttpOptions>(options), rejectAllRequests, rejectRequest);

            var httpContext = new DefaultHttpContext();
            await middleware.Invoke(httpContext);

            Assert.True(rejectOverrideCalled);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (HttpStatusCode)httpContext.Response.StatusCode);
        }

        private class TestHttpThrottleMiddleware : HttpThrottleMiddleware
        {
            private readonly Func<bool> _rejectAllRequests;
            private readonly Action<HttpContext> _rejectRequest;

            public TestHttpThrottleMiddleware(RequestDelegate next, IOptions<HttpOptions> options, Func<bool> rejectAllRequests = null, Action<HttpContext> rejectRequest = null)
                : base(next, options, null)
            {
                _rejectAllRequests = rejectAllRequests;
                _rejectRequest = rejectRequest;
            }

            public override Task Invoke(HttpContext httpContext)
            {
                if (_rejectAllRequests != null && _rejectAllRequests())
                {
                    RejectRequest(httpContext);
                    return Task.CompletedTask;
                }

                return base.Invoke(httpContext);
            }

            protected override void RejectRequest(HttpContext httpContext)
            {
                if (_rejectRequest != null)
                {
                    _rejectRequest(httpContext);
                    return;
                }

                base.RejectRequest(httpContext);
            }
        }
    }
}
