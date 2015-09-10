using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace Microsoft.Azure.WebJobs.Extensions.WebHooks
{
    internal class WebHookListener : IListener
    {
        private readonly WebHookDispatcher _dispatcher;
        private readonly MethodInfo _method;
        private readonly Uri _route;
        private readonly ITriggeredFunctionExecutor _executor;

        public WebHookListener(WebHookDispatcher dispatcher, MethodInfo method, Uri route, ITriggeredFunctionExecutor executor)
        {
            _dispatcher = dispatcher;
            _method = method;
            _route = route;
            _executor = executor;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            string methodName = string.Format("{0}.{1}", _method.DeclaringType, _method.Name);
            await _dispatcher.RegisterRoute(_route, methodName, _executor);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _dispatcher.UnregisterRoute(_route);
        }

        public void Dispose()
        {
            _dispatcher.Dispose();
        }

        public void Cancel()
        {
        }
    }
}
