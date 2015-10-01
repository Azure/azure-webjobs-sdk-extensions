// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.WebHooks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Newtonsoft.Json.Linq;
using SuaveServerWrapper;

namespace Microsoft.Azure.WebJobs.Extensions.WebHooks
{
    /// <summary>
    /// This class maintains a single http listener that listens for web hook requests
    /// for the entire <see cref="JobHost"/>. When it receives requests, it dispatches them to the correct
    /// job function.
    /// </summary>
    internal class WebHookDispatcher : IDisposable
    {
        private readonly TraceWriter _trace;
        private readonly int _port;
        private readonly Type[] _types;
        private readonly ConcurrentDictionary<string, MethodInfo> _methodNameMap = new ConcurrentDictionary<string, MethodInfo>();
        private readonly JobHost _host;
        private WebHookReceiverManager _webHookReceiverManager;

        private ConcurrentDictionary<string, ITriggeredFunctionExecutor> _functions;
        private HttpHost _httpHost;

        public WebHookDispatcher(WebHooksConfiguration webHooksConfig, JobHost host, JobHostConfiguration config, TraceWriter trace)
        {
            _functions = new ConcurrentDictionary<string, ITriggeredFunctionExecutor>();
            _trace = trace;
            _port = webHooksConfig.Port;
            _types = config.TypeLocator.GetTypes().ToArray();
            _host = host;
            _webHookReceiverManager = new WebHookReceiverManager(_trace);
        }

        internal int Port
        {
            get
            {
                return _port;
            }
        }

        public async Task RegisterRoute(Uri route, ParameterInfo triggerParameter, ITriggeredFunctionExecutor executor)
        {
            await EnsureServerOpen();

            string routeKey = route.LocalPath.ToLowerInvariant();

            if (_functions.ContainsKey(routeKey))
            {
                throw new InvalidOperationException(string.Format("Duplicate route detected. There is already a route registered for '{0}'", routeKey));
            }

            _functions.AddOrUpdate(routeKey, executor, (k, v) => { return executor; });

            WebHookTriggerAttribute attribute = triggerParameter.GetCustomAttribute<WebHookTriggerAttribute>();
            IWebHookReceiver receiver = null;
            string receiverId = string.Empty;
            string receiverLog = string.Empty;
            if (attribute != null && _webHookReceiverManager.TryParseReceiver(route.LocalPath, out receiver, out receiverId))
            {
                receiverLog = string.Format(" (Receiver: '{0}', Id: '{1}')", receiver.Name, receiverId);
            }

            MethodInfo method = (MethodInfo)triggerParameter.Member;
            string methodName = string.Format("{0}.{1}", method.DeclaringType, method.Name);
            _trace.Verbose(string.Format("WebHook route '{0}' registered for function '{1}'{2}", route.LocalPath, methodName, receiverLog));
        }

        public Task UnregisterRoute(Uri route)
        {
            string routeKey = route.LocalPath.ToLowerInvariant();

            ITriggeredFunctionExecutor executor = null;
            _functions.TryRemove(routeKey, out executor);

            if (_functions.Count == 0)
            {
                // routes are only unregistered when function listeners are
                // shutting down, so we ref count here and when the last one
                // is removed, we stop the server
                _httpHost.Close();
            }

            return Task.FromResult(0);
        }

        private async Task<HttpResponseMessage> OnRequest(HttpRequestMessage request)
        {
            try
            {
                _trace.Verbose(string.Format("Http request received: {0} {1}", request.Method, request.RequestUri));

                return await ProcessRequest(request);
            }
            catch
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        private async Task<HttpResponseMessage> ProcessRequest(HttpRequestMessage request)
        {
            // First check if we have a WebHook function registered for this route
            string routeKey = request.RequestUri.LocalPath.ToLowerInvariant();
            ITriggeredFunctionExecutor executor = null;
            HttpResponseMessage response = null;
            if (!_functions.TryGetValue(routeKey, out executor))
            {
                if (request.Method != HttpMethod.Post)
                {
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }

                // No WebHook function is registered for this route
                // See see if there is a job function that matches based
                // on name, and if so invoke it directly
                response = await TryInvokeNonWebHook(routeKey, request);
            }
            else
            {
                // Define a function to invoke the job function that we can reuse below
                Func<HttpRequestMessage, Task<HttpResponseMessage>> invokeJobFunction = async (req) =>
                {
                    TriggeredFunctionData data = new TriggeredFunctionData
                    {
                        TriggerValue = req
                    };
                    FunctionResult result = await executor.TryExecuteAsync(data, CancellationToken.None);

                    object value = null;
                    if (request.Properties.TryGetValue(WebHookTriggerBinding.WebHookContextRequestKey, out value))
                    {
                        // If this is a WebHookContext binding, see if a custom response has been set.
                        // If so, we'll return that.
                        WebHookContext context = (WebHookContext)value;
                        if (context.Response != null)
                        {
                            response = context.Response;
                            response.RequestMessage = request;
                        }
                    }

                    if (response != null)
                    {
                        return response;
                    }
                    else
                    {
                        return new HttpResponseMessage(result.Succeeded ? HttpStatusCode.OK : HttpStatusCode.InternalServerError);
                    }
                };

                // See if there is a WebHookReceiver registered for this request
                // Note: receivers will do their own HttpMethod validation (e.g. some
                // support HEAD/GET/etc.
                response = await _webHookReceiverManager.TryHandle(request, invokeJobFunction);

                if (response == null)
                {
                    if (request.Method != HttpMethod.Post)
                    {
                        return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                    }

                    // No WebHook receivers have been registered for this request, so dispatch
                    // it directly.
                    response = await invokeJobFunction(request);
                }
            }

            return response;
        }

        private async Task<HttpResponseMessage> TryInvokeNonWebHook(string routeKey, HttpRequestMessage request)
        {
            // If no WebHook function is registered, we'll see if there is a job function
            // that matches based on name, and if so invoke it directly
            MethodInfo methodInfo = null;
            if (TryGetMethodInfo(routeKey, out methodInfo))
            {
                // Read the method arguments from the request body
                // and invoke the function
                string body = await request.Content.ReadAsStringAsync();
                IDictionary<string, JToken> parsed = JObject.Parse(body);
                IDictionary<string, object> args = parsed.ToDictionary(p => p.Key, q => (object)q.Value.ToString());
                await _host.CallAsync(methodInfo, args);

                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private bool TryGetMethodInfo(string routeKey, out MethodInfo methodInfo)
        {
            string methodName = routeKey.Trim('/').Replace('/', '.');
            if (_methodNameMap.TryGetValue(methodName, out methodInfo))
            {
                return true;
            }
            else
            {
                foreach (Type type in _types)
                {
                    foreach (MethodInfo currMethod in type.GetMethods())
                    {
                        string currMethodName = string.Format("{0}.{1}", currMethod.DeclaringType.Name, currMethod.Name);
                        if (currMethodName.EndsWith(methodName, StringComparison.OrdinalIgnoreCase))
                        {
                            methodInfo = currMethod;
                            _methodNameMap[methodName] = methodInfo;
                            return true;
                        }
                    }
                }
                _methodNameMap[methodName] = null;
                return false;
            }
        }

        private async Task EnsureServerOpen()
        {
            // On startup all WebHook listeners will call into this function.
            // We only need to create the server once - it is shared by all.
            if (_httpHost == null)
            {
                _httpHost = new HttpHost(_port);
                await _httpHost.OpenAsync(OnRequest);

                _trace.Verbose(string.Format("Opened HTTP server on port {0}", _port));
            }
        }

        public void Dispose()
        {
            if (_httpHost != null)
            {
                ((IDisposable)_httpHost).Dispose();
                _httpHost = null;
            }

            if (_webHookReceiverManager != null)
            {
                ((IDisposable)_webHookReceiverManager).Dispose();
                _webHookReceiverManager = null;
            }
        }
    }
}
