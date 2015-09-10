using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
        private ConcurrentDictionary<string, ITriggeredFunctionExecutor> _functions;
        private readonly TraceWriter _trace;
        private readonly int _port;
        private HttpHost _httpHost;
        private readonly Type[] _types;
        private readonly ConcurrentDictionary<string, MethodInfo> _methodNameMap = new ConcurrentDictionary<string, MethodInfo>();
        private readonly JobHost _host;

        public WebHookDispatcher(WebHooksConfiguration webHooksConfig, JobHost host, JobHostConfiguration config, TraceWriter trace)
        {
            _functions = new ConcurrentDictionary<string, ITriggeredFunctionExecutor>();
            _trace = trace;
            _port = webHooksConfig.Port;
            _types = config.TypeLocator.GetTypes().ToArray();
            _host = host;
        }

        internal int Port
        {
            get
            {
                return _port;
            }
        }

        public async Task RegisterRoute(Uri route, string methodName, ITriggeredFunctionExecutor executor)
        {
            await EnsureServerOpen();

            string routeKey = route.LocalPath.ToLowerInvariant();

            if (_functions.ContainsKey(routeKey))
            {
                throw new InvalidOperationException(string.Format("Duplicate route detected. There is already a route registered for '{0}'", routeKey));
            }

            _functions.AddOrUpdate(routeKey, executor, 
                (k, v) => { return executor; });

            _trace.Verbose(string.Format("WebHook route '{0}' registered for function '{1}'", route.LocalPath, methodName));
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
                if (request.Method != HttpMethod.Post)
                {
                    return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                }

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
            // First check if we have a WebHook function registered for
            // this route
            string routeKey = request.RequestUri.LocalPath.ToLowerInvariant();
            ITriggeredFunctionExecutor executor = null;
            if (!_functions.TryGetValue(routeKey, out executor))
            {
                // If no WebHook function is registered, we'll see if there is a job function
                // that matches based on name, and if so invoke it directly
                MethodInfo methodInfo = null;
                if (TryGetMethodInfo(routeKey, out methodInfo))
                {
                    // Read the method arguments from the request body
                    string body = await request.Content.ReadAsStringAsync();
                    IDictionary<string, JToken> parsed = JObject.Parse(body);
                    IDictionary<string, object> args = parsed.ToDictionary(p => p.Key, q => (object)q.Value.ToString());
                    await _host.CallAsync(methodInfo, args);

                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            TriggeredFunctionData data = new TriggeredFunctionData
            {
                TriggerValue = request
            };
            FunctionResult result = await executor.TryExecuteAsync(data, CancellationToken.None);

            HttpStatusCode statusCode = result.Succeeded ? HttpStatusCode.OK : HttpStatusCode.InternalServerError;

            return new HttpResponseMessage(statusCode);
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
        }
    }
}
