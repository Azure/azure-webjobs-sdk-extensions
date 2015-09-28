// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using Autofac;
using Autofac.Integration.WebApi;
using Microsoft.AspNet.WebHooks;
using Microsoft.AspNet.WebHooks.Diagnostics;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions.WebHooks
{
    /// <summary>
    /// Custom <see cref="WebHookHandler"/> used to integrate ASP.NET WebHooks into the WebJobs
    /// WebHook request pipeline.
    /// When a request is dispatched to a <see cref="WebHookReceiver"/>, after validating the request
    /// fully, it will delegate to this handler, allowing us to resume processing.
    /// </summary>
    internal class WebJobsWebHookHandler : WebHookHandler, IDisposable
    {
        internal const string WebHookJobFunctionInvokerKey = "WebHookJobFunctionInvoker";

        private readonly TraceWriter _trace;
        private readonly Collection<WebHookReceiver> _webHookReceivers;
        private HttpConfiguration _httpConfiguration;

        public WebJobsWebHookHandler(Collection<WebHookReceiver> webHookReceivers, TraceWriter trace)
        {
            _trace = trace;
            _httpConfiguration = new HttpConfiguration();
            _webHookReceivers = webHookReceivers;
        }

        public void Initialize()
        {
            // set up the IOC container
            var builder = new ContainerBuilder();
            ILogger logger = new WebHooksLogger(_trace);
            builder.RegisterInstance<ILogger>(logger);
            builder.RegisterInstance<IWebHookHandler>(this);
            var container = builder.Build();

            // build the HttpConfiguration
            _httpConfiguration.DependencyResolver = new AutofacWebApiDependencyResolver(container);
        }

        public async Task<HttpResponseMessage> TryHandle(HttpRequestMessage request, Func<HttpRequestMessage, Task<HttpResponseMessage>> invokeJobFunction)
        {
            // First check if there is a registered WebHook Receiver for this request, and if
            // so use it
            string route = request.RequestUri.LocalPath.ToLowerInvariant();
            string[] routeSegements = route.ToLowerInvariant().TrimStart('/').Split('/');
            if (routeSegements.Length == 1 || routeSegements.Length == 2)
            {
                WebHookReceiver webHookReceiver = _webHookReceivers
                    .FirstOrDefault(p => string.Compare(p.Name, routeSegements[0], StringComparison.OrdinalIgnoreCase) == 0);

                if (webHookReceiver != null)
                {
                    // parse the optional WebHook ID from the route if specified
                    string id = string.Empty;
                    if (routeSegements.Length == 2)
                    {
                        id = routeSegements[1];
                    }

                    HttpRequestContext context = new HttpRequestContext
                    {
                        Configuration = _httpConfiguration
                    };
                    request.SetConfiguration(_httpConfiguration);

                    // add the anonymous handler function from above to the request properties
                    // so our custom WebHookHandler can invoke it at the right time
                    request.Properties.Add(WebHookJobFunctionInvokerKey, invokeJobFunction);

                    return await webHookReceiver.ReceiveAsync(id, context, request);
                }
            }

            return null;
        }

        public override async Task ExecuteAsync(string receiver, WebHookHandlerContext context)
        {
            // At this point, the WebHookReceiver has validated this request, so we
            // now need to dispatch it to the actual job function.

            // get the request handler from message properties
            var requestHandler = (Func<HttpRequestMessage, Task<HttpResponseMessage>>)
                context.Request.Properties[WebHookJobFunctionInvokerKey];

            // Invoke the job function
            context.Response = await requestHandler(context.Request);
        }

        public void Dispose()
        {
            if (_httpConfiguration != null)
            {
                ((IDisposable)_httpConfiguration).Dispose();
                _httpConfiguration = null;
            }
        }
    }
}
